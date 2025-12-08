using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BookingCreationTest;

/// <summary>
/// Test script to simulate booking creation process as it happens in chat
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Booking Creation Test ===");

        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("MicrohireAgentChat/appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        // Setup DI container
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(config => config.AddConsole());

        // Add database context
        services.AddDbContext<BookingDbContext>(opt =>
        {
            var cs = configuration.GetConnectionString("BookingsDb");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Missing connection string 'BookingsDb'.");
            opt.UseSqlServer(cs);
            opt.EnableDetailedErrors();
            opt.EnableSensitiveDataLogging();
        });

        // Register services (same as Program.cs)
        services.AddScoped<ConversationExtractionService>();
        services.AddScoped<ContactPersistenceService>();
        services.AddScoped<OrganizationPersistenceService>();
        services.AddScoped<BookingPersistenceService>();
        services.AddScoped<ItemPersistenceService>();
        services.AddScoped<CrewPersistenceService>();
        services.AddScoped<BookingOrchestrationService>();

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var orchestrationService = scope.ServiceProvider.GetRequiredService<BookingOrchestrationService>();

            // Test database connection
            Console.WriteLine("Testing database connection...");
            await db.Database.CanConnectAsync();
            Console.WriteLine("✓ Database connection successful");

            // Create sample conversation messages
            var messages = CreateSampleConversation();
            Console.WriteLine($"Created {messages.Count()} sample messages");

            // Process the conversation (this is what happens when user confirms booking)
            Console.WriteLine("\nProcessing conversation...");
            var result = await orchestrationService.ProcessConversationAsync(messages, null, CancellationToken.None);

            // Report results
            Console.WriteLine("\n=== RESULTS ===");
            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"Booking No: {result.BookingNo}");
            Console.WriteLine($"Contact ID: {result.ContactId}");
            Console.WriteLine($"Organization ID: {result.OrganizationId}");
            Console.WriteLine($"Customer Code: {result.CustomerCode}");

            if (result.Errors.Any())
            {
                Console.WriteLine("\nErrors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"- {error}");
                }
            }

            // Verify booking was created
            if (!string.IsNullOrWhiteSpace(result.BookingNo))
            {
                var booking = await db.TblBookings
                    .FirstOrDefaultAsync(b => b.booking_no == result.BookingNo);

                if (booking != null)
                {
                    Console.WriteLine("\n=== BOOKING VERIFICATION ===");
                    Console.WriteLine($"Booking found: {booking.booking_no}");
                    Console.WriteLine($"Event Date: {booking.SDate?.ToString("yyyy-MM-dd")}");
                    Console.WriteLine($"Venue ID: {booking.VenueID}");
                    Console.WriteLine($"Contact Name: {booking.contact_nameV6}");
                    Console.WriteLine($"Organization: {booking.OrganizationV6}");
                    Console.WriteLine($"Price Quoted: {booking.price_quoted}");
                    Console.WriteLine($"Status: {booking.status}");
                    Console.WriteLine($"Booking Type: {booking.booking_type_v32}");
                }
                else
                {
                    Console.WriteLine("❌ Booking not found in database!");
                }
            }

            Console.WriteLine("\n=== TEST COMPLETE ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Create sample conversation messages that would result from a typical booking chat
    /// </summary>
    private static IEnumerable<DisplayMessage> CreateSampleConversation()
    {
        var now = DateTimeOffset.Now;
        var messages = new List<DisplayMessage>();

        // User introduces themselves
        messages.Add(new DisplayMessage("user", now.AddMinutes(-10), new[]
        {
            "Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event."
        }));

        // Assistant asks for details
        messages.Add(new DisplayMessage("assistant", now.AddMinutes(-9), new[]
        {
            "Hello Michael! I'd be happy to help you book equipment for your event. Could you please tell me:\n\n1. When is the event?\n2. Where is the venue?\n3. What equipment do you need?\n4. How many people will attend?"
        }));

        // User provides event details
        messages.Add(new DisplayMessage("user", now.AddMinutes(-8), new[]
        {
            "The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees."
        }));

        // Assistant asks for more details
        messages.Add(new DisplayMessage("assistant", now.AddMinutes(-7), new[]
        {
            "Great! So we have:\n- Event: 15 March 2025\n- Venue: Westin Melbourne\n- Attendees: 100\n\nWhat equipment do you need? For example: sound system, lighting, staging, etc."
        }));

        // User provides equipment needs
        messages.Add(new DisplayMessage("user", now.AddMinutes(-6), new[]
        {
            "We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area."
        }));

        // Assistant asks for contact details
        messages.Add(new DisplayMessage("assistant", now.AddMinutes(-5), new[]
        {
            "Perfect! Let me get your contact details for the booking.\n\nWhat's your phone number and email address?"
        }));

        // User provides contact info
        messages.Add(new DisplayMessage("user", now.AddMinutes(-4), new[]
        {
            "My email is michael@yes100attendees.com and phone is 07111111111. I'm the Events Coordinator."
        }));

        // Assistant confirms setup times
        messages.Add(new DisplayMessage("assistant", now.AddMinutes(-3), new[]
        {
            "Thanks Michael! Now for the logistics:\n\n- Setup time: 8:00 AM\n- Show start: 6:00 PM\n- Show end: 10:00 PM\n- Strike time: 11:00 PM\n\nDoes this work for you?"
        }));

        // User confirms
        messages.Add(new DisplayMessage("user", now.AddMinutes(-2), new[]
        {
            "Yes, that timing works perfectly."
        }));

        // Assistant provides quote
        messages.Add(new DisplayMessage("assistant", now.AddMinutes(-1), new[]
        {
            "Based on your requirements, here's the quote:\n\n**Equipment:**\n- Sound System: $2,500\n- Lighting Setup: $1,800\n- Staging: $900\n\n**Labor:**\n- Setup Crew (2 people, 2 hours): $400\n- Show Crew (3 people, 4 hours): $1,200\n- Strike Crew (2 people, 1 hour): $200\n\n**Total: $6,900** (includes GST)\n\nShall I proceed with creating this booking?"
        }));

        // User confirms booking
        messages.Add(new DisplayMessage("user", now, new[]
        {
            "Yes, please create the booking."
        }));

        return messages;
    }
}
