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
        await page.screenshot({ path: 'screenshots/01-initial-load.png', fullPage: true });
        
        console.log('Step 2: Starting conversation to set room as Thrive Boardroom...');
        
        // Find the input field and send button
        const inputSelector = 'input[type="text"], textarea, [contenteditable="true"]';
        await page.waitForSelector(inputSelector, { timeout: 10000 });
        
        // Send first message to quickly get to Thrive Boardroom
        await page.fill(inputSelector, 'I need AV for a meeting in Thrive Boardroom tomorrow');
        await page.click('button[type="submit"], button:has-text("Send")');
        await page.waitForTimeout(3000);
        await page.screenshot({ path: 'screenshots/02-first-message.png', fullPage: true });
        
        // Wait for bot response
        await page.waitForTimeout(5000);
        
        // Continue conversation to reach AV stage
        await page.fill(inputSelector, 'yes');
        await page.click('button[type="submit"], button:has-text("Send")');
        await page.waitForTimeout(5000);
        await page.screenshot({ path: 'screenshots/03-confirmed-room.png', fullPage: true });
        
        console.log('Step 3: Asking "yeah what does it look like"...');
        await page.fill(inputSelector, 'yeah what does it look like');
        await page.click('button[type="submit"], button:has-text("Send")');
        await page.waitForTimeout(3000);
        await page.screenshot({ path: 'screenshots/04-what-does-it-look-like.png', fullPage: true });
        
        // Wait for response
        await page.waitForTimeout(8000);
        await page.screenshot({ path: 'screenshots/05-room-view-response.png', fullPage: true });
        
        console.log('Step 4: Checking response content...');
        
        // Get all message content
        const messages = await page.evaluate(() => {
            const msgElements = document.querySelectorAll('.message, .assistant-message, .bot-message, [class*="message"]');
            return Array.from(msgElements).map(el => el.textContent.trim());
        });
        
        const lastMessages = messages.slice(-3).join(' ');
        console.log('Last messages:', lastMessages);
        
        // Check for images
        const images = await page.evaluate(() => {
            const imgs = document.querySelectorAll('img');
            return Array.from(imgs).map(img => ({
                src: img.src,
                alt: img.alt,
                visible: img.offsetWidth > 0 && img.offsetHeight > 0
            }));
        });
        
        console.log('Images found:', images.length);
        
        // Check for setup cards (Theatre/Cabaret)
        const hasSetupCards = lastMessages.toLowerCase().includes('theatre') || 
                             lastMessages.toLowerCase().includes('cabaret') ||
                             lastMessages.toLowerCase().includes('classroom') ||
                             lastMessages.toLowerCase().includes('u-shape');
        
        const hasRoomImage = images.some(img => img.visible && (
            img.src.includes('thrive') || 
            img.alt.toLowerCase().includes('thrive') ||
            img.alt.toLowerCase().includes('boardroom')
        ));
        
        results.tests.push({
            name: 'Thrive image should be cover-only (no layout options)',
            status: !hasSetupCards && hasRoomImage ? 'PASS' : 'FAIL',
            details: {
                hasSetupCards,
                hasRoomImage,
                imageCount: images.filter(img => img.visible).length,
                responseSnippet: lastMessages.substring(0, 200)
            }
        });
        
        console.log('Step 5: Continuing to AV summary confirmation...');
        await page.fill(inputSelector, 'yes that looks good');
        await page.click('button[type="submit"], button:has-text("Send")');
        await page.waitForTimeout(5000);
        await page.screenshot({ path: 'screenshots/06-av-confirmation.png', fullPage: true });
        
        // Wait for next response
        await page.waitForTimeout(8000);
        await page.screenshot({ path: 'screenshots/07-after-confirmation.png', fullPage: true });
        
        // Get messages again
        const messagesAfterConfirm = await page.evaluate(() => {
            const msgElements = document.querySelectorAll('.message, .assistant-message, .bot-message, [class*="message"]');
            return Array.from(msgElements).map(el => el.textContent.trim());
        });
        
        const lastMessagesAfterConfirm = messagesAfterConfirm.slice(-3).join(' ');
        console.log('Messages after confirmation:', lastMessagesAfterConfirm);
        
        // Check if bot asks about setup style for Thrive
        const asksSetupStyle = lastMessagesAfterConfirm.toLowerCase().includes('setup') && 
                              lastMessagesAfterConfirm.toLowerCase().includes('style') ||
                              lastMessagesAfterConfirm.toLowerCase().includes('theatre') ||
                              lastMessagesAfterConfirm.toLowerCase().includes('cabaret') ||
                              lastMessagesAfterConfirm.toLowerCase().includes('how would you like');
        
        results.tests.push({
            name: 'No setup-style follow-up question for Thrive',
            status: !asksSetupStyle ? 'PASS' : 'FAIL',
            details: {
                asksSetupStyle,
                responseSnippet: lastMessagesAfterConfirm.substring(0, 200)
            }
        });
        
        console.log('\n=== TEST RESULTS ===');
        results.tests.forEach(test => {
            console.log(`${test.status}: ${test.name}`);
            console.log(`Details:`, JSON.stringify(test.details, null, 2));
        });
        
        // Save results
        fs.writeFileSync('smoke-test-results.json', JSON.stringify(results, null, 2));
        
    } catch (error) {
        console.error('Error during test:', error);
        results.error = error.message;
        await page.screenshot({ path: 'screenshots/error.png', fullPage: true });
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
    console.log('\nTest completed. Results saved to smoke-test-results.json');
    process.exit(results.tests.every(t => t.status === 'PASS') ? 0 : 1);
}).catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
