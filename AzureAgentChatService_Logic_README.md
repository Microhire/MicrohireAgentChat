# AzureAgentChatService.cs - Core Logic Overview

## Overview
The `AzureAgentChatService.cs` is a monolithic service (~4,500 lines) that serves as the bridge between a web application, Azure AI Agent API, and a local SQL database. It implements AI-driven backend-for-frontend logic with extensive business rule automation.

## Core Architecture

### 1. Agent Orchestration (`SendAsync`, `RunAgentAndHandleToolsAsync`)

**Main Message Flow:**
- **Session Management**: Maintains `ThreadId` in user HTTP session for conversation continuity
- **Local Interception**: Checks for specific UI interactions (schedule/time selection) to handle locally without agent round-trips
- **Agent Execution Loop**:
  - Sends user messages to Azure AI Agent
  - Polls run status: `Queued` → `InProgress` → `Completed`/`RequiresAction`
  - **Tool Dispatcher**: When agent requires action, switches on function name and executes corresponding C# logic
- **Retry Logic**: Robust `With429RetryAsync` wrapper handles Azure API throttling (HTTP 429)

### 2. Tools Exposed to the Agent

**Schedule Recording:**
- `check_date_availability`: Records the event date and time window for scheduling (no RP database validation; delegates to AgentToolHandlerService)

**Product/Catalog Tools:**
- `get_product_info`: Searches `TblInvmas` (inventory) by product code or keyword
- `get_product_images`: Retrieves product images with metadata from inventory

**Venue/Room Tools:**
- `list_westin_rooms`: Returns static room catalog from `IWestinRoomCatalog`
- `get_room_images`: Fetches room layouts and images for specific venues

**UI Interaction Tools:**
- `build_time_picker`: Generates JSON payloads for frontend time selection widgets
- `generate_quote`: Creates HTML/PDF quotes using static template `StaticQuoteHtml()`

## 3. Heuristic Data Extraction (Regex-Driven)

**Date/Time Extraction (`ExtractEventDate`):**
- Multiple regex patterns for various date formats
- Handles US/AU/UK date conventions
- Supports relative dates and year inference

**Contact Information (`ExtractContactInfo`):**
- Email extraction with regex patterns
- Name detection using proximity heuristics (near email/phone)
- Phone number parsing with Australian normalization
- Position/title extraction from free text

**Organization Parsing (`ExtractOrganisationFromTranscript`):**
- Handles patterns like "Company: ACME Corp" or "from ACME Corp"
- Address extraction alongside organization names
- Case-insensitive matching with normalization

**Booking Fields (`ExtractExpectedFields`):**
- Scrapes transcript for key-value pairs:
  - Show Start/End Time
  - Booking Type, Status
  - Room, Venue Name
  - Contact Name, Organization
  - Equipment costs, labor costs

## 4. Database Persistence (ERP Integration)

### Contact Management (`UpsertContactByEmailAsync`)
- Creates/updates `TblContact` records
- Name splitting (First/Middle/Last)
- Email deduplication
- Phone normalization to E164 format
- Position/title storage

### Organization Management (`UpsertOrganisationAsync`)
- Creates customer records in `TblCust`
- Generates unique customer codes (C##### format)
- Handles organization name/address linking
- Transaction-safe with temporary code generation

### Booking Creation (`TrySaveBookingAsync`)
- Orchestrates main booking record creation
- Maps extracted fields to `TblBooking` columns
- Handles time conversions (HHmm format)
- Links contacts and organizations

### Equipment/Item Management (`UpsertItemsFromSummaryAsync`)
- **Complex Rule Engine**: Reads `item-rules.json` for business logic
- **Package Handling**: Supports parent packages (ItemType=1) with components (ItemType=2)
- **Quantity Calculation**: Parses natural language (e.g., "2 laptops" → PCLPRO package)
- **Rate Lookup**: Queries `TblRatetbls` for pricing

### Crew Labor Management (`InsertCrewRowsAsync`)
- Calculates labor requirements based on event duration
- Creates crew rows for Setup/Packdown/Rehearsal/Tech support
- Time calculations with configurable person-hours
- Links to booking with proper sequencing

### Conversation Archiving (`SaveFullTranscriptToBooknoteAsync`)
- Saves complete chat history to `TblBooknote`
- Formats messages as "Agent: ..." / "User: ..." pairs
- Maintains conversation context for booking records

## 5. Business Logic Specifics

### Draft State Management
- Temporary storage in HTTP Session (`Draft:StartTime`, `Draft:EndTime`, etc.)
- Allows multi-step data collection before database commit

### Equipment Rules Engine
- JSON-driven rules for product mapping
- Supports bundles, drivers, and conditional logic
- Handles presentation equipment inference

### Time Zone Handling
- Explicit AEST (Australian Eastern Standard Time) conversions
- Consistent timezone handling across all date operations

### Error Handling & Resilience
- Comprehensive try/catch with specific exception types
- Database transaction management
- Graceful degradation for non-critical operations

## Key Design Patterns

1. **Heuristic Fallbacks**: Regex extraction as backup when AI tools fail
2. **Session-Based State**: HTTP session for temporary conversation state
3. **Rule-Driven Processing**: JSON configuration for business logic
4. **Transactional Safety**: Database operations wrapped in transactions
5. **Polymorphic Persistence**: Different handling for packages vs. individual items

## Integration Points

- **Azure AI Agents**: Core chat functionality
- **Entity Framework**: Database operations via `BookingDbContext`
- **HTTP Context**: Session management and request context
- **Static Data**: Room catalogs, product images
- **File System**: Quote PDF generation, rules loading

This service effectively transforms conversational AI interactions into structured business data, automating the quote-to-booking pipeline with extensive fallback logic for reliability.
