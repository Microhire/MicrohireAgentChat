const { chromium } = require('playwright');
const fs = require('fs');

async function runSmokeTest() {
    const browser = await chromium.launch({ headless: false });
    const context = await browser.newContext();
    const page = await context.newPage();
    
    const results = {
        timestamp: new Date().toISOString(),
        tests: []
    };
    
    try {
        console.log('Step 1: Opening http://localhost:5216...');
        await page.goto('http://localhost:5216', { waitUntil: 'networkidle' });
        await page.waitForTimeout(2000);
        await page.screenshot({ path: 'screenshots/v2-01-initial-load.png', fullPage: true });
        
        console.log('Step 2: Starting conversation to set room as Thrive Boardroom...');
        
        // Find the input field
        const inputSelector = '#userInput, input[type="text"], textarea';
        await page.waitForSelector(inputSelector, { timeout: 10000 });
        
        // Send first message
        await page.fill(inputSelector, 'I need AV for a meeting in Thrive Boardroom tomorrow');
        
        // Find and click send button
        const sendButton = await page.locator('button:has-text("Send"), button[type="submit"], #sendButton').first();
        await sendButton.click();
        await page.waitForTimeout(3000);
        await page.screenshot({ path: 'screenshots/v2-02-first-message.png', fullPage: true });
        
        // Wait for bot response
        await page.waitForTimeout(8000);
        await page.screenshot({ path: 'screenshots/v2-03-bot-response.png', fullPage: true });
        
        // Continue conversation
        await page.fill(inputSelector, 'yes');
        await sendButton.click();
        await page.waitForTimeout(8000);
        await page.screenshot({ path: 'screenshots/v2-04-confirmed.png', fullPage: true });
        
        console.log('Step 3: Asking "yeah what does it look like"...');
        await page.fill(inputSelector, 'yeah what does it look like');
        await sendButton.click();
        await page.waitForTimeout(3000);
        await page.screenshot({ path: 'screenshots/v2-05-asked-what-looks-like.png', fullPage: true });
        
        // Wait for response with room image
        await page.waitForTimeout(12000);
        await page.screenshot({ path: 'screenshots/v2-06-room-view-response.png', fullPage: true });
        
        console.log('Step 4: Checking response content...');
        
        // Get the HTML content to analyze
        const pageContent = await page.content();
        
        // Extract assistant messages more carefully
        const assistantMessages = await page.evaluate(() => {
            const messages = [];
            
            // Try multiple selectors for assistant messages
            const selectors = [
                '.assistant-message',
                '.bot-message', 
                '[class*="assistant"]',
                '[class*="bot"]',
                '.message.assistant',
                '.message:not(.user-message)'
            ];
            
            for (const selector of selectors) {
                const elements = document.querySelectorAll(selector);
                if (elements.length > 0) {
                    elements.forEach(el => {
                        const text = el.innerText || el.textContent;
                        if (text && text.trim()) {
                            messages.push({
                                text: text.trim(),
                                html: el.innerHTML.substring(0, 500)
                            });
                        }
                    });
                    break;
                }
            }
            
            // If no messages found, get all divs with substantial text
            if (messages.length === 0) {
                const allDivs = document.querySelectorAll('div');
                allDivs.forEach(div => {
                    const text = div.innerText;
                    if (text && text.length > 20 && text.length < 2000) {
                        messages.push({
                            text: text.trim(),
                            html: div.innerHTML.substring(0, 500)
                        });
                    }
                });
            }
            
            return messages;
        });
        
        console.log(`Found ${assistantMessages.length} assistant messages`);
        
        // Get the last few messages
        const recentMessages = assistantMessages.slice(-5);
        console.log('\nRecent messages:');
        recentMessages.forEach((msg, i) => {
            console.log(`\nMessage ${i + 1}:`);
            console.log(msg.text.substring(0, 300));
        });
        
        // Check for images in the page
        const images = await page.evaluate(() => {
            const imgs = Array.from(document.querySelectorAll('img'));
            return imgs.map(img => ({
                src: img.src,
                alt: img.alt || '',
                width: img.width,
                height: img.height,
                visible: img.offsetWidth > 0 && img.offsetHeight > 0,
                className: img.className
            })).filter(img => img.visible && img.width > 50 && img.height > 50);
        });
        
        console.log(`\nFound ${images.length} visible images`);
        images.forEach((img, i) => {
            console.log(`Image ${i + 1}: ${img.src.substring(0, 100)} (${img.width}x${img.height})`);
        });
        
        // Combine recent message text
        const combinedText = recentMessages.map(m => m.text).join(' ').toLowerCase();
        
        // Check for setup card keywords
        const setupKeywords = ['theatre', 'cabaret', 'classroom', 'u-shape', 'boardroom style', 'setup style'];
        const hasSetupCards = setupKeywords.some(keyword => combinedText.includes(keyword));
        
        // Check for room-related images
        const hasRoomImage = images.some(img => 
            img.src.toLowerCase().includes('thrive') || 
            img.alt.toLowerCase().includes('thrive') ||
            img.alt.toLowerCase().includes('boardroom') ||
            img.src.includes('/api/') // API-generated images
        );
        
        console.log(`\nSetup cards detected: ${hasSetupCards}`);
        console.log(`Room image detected: ${hasRoomImage}`);
        
        // Get the specific response to "what does it look like"
        const whatLooksLikeResponse = recentMessages.find(m => 
            m.text.length > 50 && 
            (m.html.includes('img') || m.text.toLowerCase().includes('thrive'))
        );
        
        results.tests.push({
            name: 'Thrive image should be cover-only (no layout options)',
            status: !hasSetupCards && hasRoomImage ? 'PASS' : 'FAIL',
            details: {
                hasSetupCards,
                hasRoomImage,
                imageCount: images.length,
                setupKeywordsFound: setupKeywords.filter(k => combinedText.includes(k)),
                responseSnippet: whatLooksLikeResponse ? whatLooksLikeResponse.text.substring(0, 300) : 'Not found',
                allImages: images.map(img => ({ src: img.src.substring(0, 100), alt: img.alt }))
            }
        });
        
        console.log('\nStep 5: Continuing to AV summary confirmation...');
        await page.fill(inputSelector, 'yes that looks good');
        await sendButton.click();
        await page.waitForTimeout(8000);
        await page.screenshot({ path: 'screenshots/v2-07-av-confirmation.png', fullPage: true });
        
        // Wait for next response
        await page.waitForTimeout(10000);
        await page.screenshot({ path: 'screenshots/v2-08-after-confirmation.png', fullPage: true });
        
        // Get messages after confirmation
        const messagesAfterConfirm = await page.evaluate(() => {
            const messages = [];
            const selectors = [
                '.assistant-message',
                '.bot-message',
                '[class*="assistant"]',
                '[class*="bot"]'
            ];
            
            for (const selector of selectors) {
                const elements = document.querySelectorAll(selector);
                if (elements.length > 0) {
                    elements.forEach(el => {
                        const text = el.innerText || el.textContent;
                        if (text && text.trim()) {
                            messages.push(text.trim());
                        }
                    });
                    break;
                }
            }
            
            return messages;
        });
        
        const lastMessagesAfterConfirm = messagesAfterConfirm.slice(-3).join(' ').toLowerCase();
        console.log('\nMessages after confirmation:');
        console.log(lastMessagesAfterConfirm.substring(0, 400));
        
        // Check if bot asks about setup style for Thrive
        const setupStyleKeywords = [
            'setup style',
            'how would you like',
            'theatre or cabaret',
            'choose a setup',
            'select a layout'
        ];
        
        const asksSetupStyle = setupStyleKeywords.some(keyword => 
            lastMessagesAfterConfirm.includes(keyword)
        );
        
        console.log(`\nAsks about setup style: ${asksSetupStyle}`);
        
        results.tests.push({
            name: 'No setup-style follow-up question for Thrive',
            status: !asksSetupStyle ? 'PASS' : 'FAIL',
            details: {
                asksSetupStyle,
                setupStyleKeywordsFound: setupStyleKeywords.filter(k => lastMessagesAfterConfirm.includes(k)),
                responseSnippet: lastMessagesAfterConfirm.substring(0, 300)
            }
        });
        
        console.log('\n\n=== FINAL TEST RESULTS ===');
        results.tests.forEach(test => {
            console.log(`\n${test.status}: ${test.name}`);
            console.log('Details:', JSON.stringify(test.details, null, 2));
        });
        
        // Save results
        fs.writeFileSync('smoke-test-results.json', JSON.stringify(results, null, 2));
        
    } catch (error) {
        console.error('Error during test:', error);
        results.error = error.message;
        await page.screenshot({ path: 'screenshots/v2-error.png', fullPage: true });
    } finally {
        await browser.close();
    }
    
    return results;
}

// Create screenshots directory
if (!fs.existsSync('screenshots')) {
    fs.mkdirSync('screenshots');
}

runSmokeTest().then(results => {
    console.log('\n\nTest completed. Results saved to smoke-test-results.json');
    const allPassed = results.tests.every(t => t.status === 'PASS');
    console.log(`\nOVERALL: ${allPassed ? 'PASS' : 'FAIL'}`);
    process.exit(allPassed ? 0 : 1);
}).catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
