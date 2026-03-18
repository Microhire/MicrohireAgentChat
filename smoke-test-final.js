const { chromium } = require('playwright');
const fs = require('fs');

async function runSmokeTest() {
    const browser = await chromium.launch({ headless: false });
    const context = await browser.newContext();
    const page = await context.newPage();
    
    const results = {
        timestamp: new Date().toISOString(),
        tests: [],
        conversationLog: []
    };
    
    // Helper to get chat messages
    async function getChatMessages() {
        return await page.evaluate(() => {
            const messages = [];
            // Target the actual chat container
            const chatContainer = document.querySelector('#messages, .messages, [class*="chat-messages"]');
            if (chatContainer) {
                const msgElements = chatContainer.querySelectorAll('.message, [class*="message-"]');
                msgElements.forEach(el => {
                    const isUser = el.classList.contains('user-message') || 
                                  el.classList.contains('user') ||
                                  el.querySelector('.user-message');
                    const isAssistant = el.classList.contains('assistant-message') || 
                                       el.classList.contains('assistant') ||
                                       el.classList.contains('bot-message');
                    
                    if (isUser || isAssistant) {
                        const text = el.innerText || el.textContent;
                        const hasImage = el.querySelector('img') !== null;
                        messages.push({
                            type: isUser ? 'user' : 'assistant',
                            text: text.trim(),
                            hasImage,
                            html: el.innerHTML.substring(0, 300)
                        });
                    }
                });
            }
            return messages;
        });
    }
    
    // Helper to send message
    async function sendMessage(text) {
        const inputSelector = '#userInput, input[type="text"], textarea';
        await page.fill(inputSelector, text);
        const sendButton = await page.locator('button:has-text("Send"), button[type="submit"], #sendButton').first();
        await sendButton.click();
        results.conversationLog.push({ type: 'user', text });
    }
    
    // Helper to wait for assistant response
    async function waitForAssistantResponse(previousCount) {
        let attempts = 0;
        while (attempts < 30) {
            const messages = await getChatMessages();
            const assistantMessages = messages.filter(m => m.type === 'assistant');
            if (assistantMessages.length > previousCount) {
                const newMessage = assistantMessages[assistantMessages.length - 1];
                results.conversationLog.push({ 
                    type: 'assistant', 
                    text: newMessage.text.substring(0, 500),
                    hasImage: newMessage.hasImage
                });
                return newMessage;
            }
            await page.waitForTimeout(1000);
            attempts++;
        }
        throw new Error('Timeout waiting for assistant response');
    }

    function containsAny(text, patterns) {
        const lower = (text || '').toLowerCase();
        return patterns.some(p => lower.includes(p));
    }
    
    try {
        console.log('=== SMOKE TEST: Thrive Boardroom Layout Validation ===\n');
        
        console.log('Step 1: Opening http://localhost:5216...');
        await page.goto('http://localhost:5216', { waitUntil: 'networkidle' });
        await page.waitForTimeout(2000);
        await page.screenshot({ path: 'screenshots/final-01-initial.png', fullPage: true });
        
        console.log('Step 2: Starting conversation to reach Thrive Boardroom...\n');
        
        let messages = await getChatMessages();
        let assistantCount = messages.filter(m => m.type === 'assistant').length;
        
        console.log('User: "I need AV for a meeting in Thrive Boardroom tomorrow"');
        await sendMessage('I need AV for a meeting in Thrive Boardroom tomorrow');
        await page.waitForTimeout(2000);
        await page.screenshot({ path: 'screenshots/final-02-first-msg.png', fullPage: true });
        
        const response1 = await waitForAssistantResponse(assistantCount);
        console.log(`Assistant: "${response1.text.substring(0, 150)}..."\n`);
        assistantCount++;
        
        await page.waitForTimeout(2000);
        
        console.log('User: "yes"');
        await sendMessage('yes');
        await page.waitForTimeout(2000);
        
        const response2 = await waitForAssistantResponse(assistantCount);
        console.log(`Assistant: "${response2.text.substring(0, 150)}..."\n`);
        assistantCount++;
        
        await page.waitForTimeout(2000);
        await page.screenshot({ path: 'screenshots/final-03-confirmed.png', fullPage: true });
        
        console.log('Step 3: Asking "yeah what does it look like"...\n');
        console.log('User: "yeah what does it look like"');
        await sendMessage('yeah what does it look like');
        await page.waitForTimeout(2000);
        await page.screenshot({ path: 'screenshots/final-04-asked.png', fullPage: true });
        
        const response3 = await waitForAssistantResponse(assistantCount);
        console.log(`Assistant: "${response3.text.substring(0, 200)}..."`);
        console.log(`Has image: ${response3.hasImage}\n`);
        assistantCount++;
        
        await page.waitForTimeout(3000);
        await page.screenshot({ path: 'screenshots/final-05-room-view.png', fullPage: true });
        
        console.log('Step 4: Analyzing response for layout options...\n');
        
        // Get all images in the chat area
        const chatImages = await page.evaluate(() => {
            const chatContainer = document.querySelector('#messages, .messages, [class*="chat-messages"]');
            if (!chatContainer) return [];
            
            const imgs = Array.from(chatContainer.querySelectorAll('img'));
            return imgs.map(img => ({
                src: img.src,
                alt: img.alt || '',
                width: img.width,
                height: img.height,
                visible: img.offsetWidth > 0 && img.offsetHeight > 0
            })).filter(img => img.visible && img.width > 50 && img.height > 50);
        });
        
        console.log(`Found ${chatImages.length} images in chat:`);
        chatImages.forEach((img, i) => {
            console.log(`  ${i + 1}. ${img.src} (${img.width}x${img.height}) - "${img.alt}"`);
        });
        
        // Check for setup keywords in response
        const responseText = response3.text.toLowerCase();
        const setupKeywords = ['theatre', 'cabaret', 'classroom', 'u-shape', 'boardroom style', 'setup style', 'layout'];
        const foundKeywords = setupKeywords.filter(k => responseText.includes(k));
        
        console.log(`\nSetup keywords found: ${foundKeywords.length > 0 ? foundKeywords.join(', ') : 'None'}`);
        
        const hasRoomImage = chatImages.some(img => 
            img.src.includes('thrive') || 
            img.alt.toLowerCase().includes('thrive') ||
            img.alt.toLowerCase().includes('room')
        );
        
        const hasSetupCards = foundKeywords.length > 0;
        
        console.log(`Room image present: ${hasRoomImage}`);
        console.log(`Setup cards/options present: ${hasSetupCards}`);
        
        const test1Pass = hasRoomImage && !hasSetupCards;
        console.log(`\n✓ TEST 1: ${test1Pass ? 'PASS' : 'FAIL'} - Thrive image should be cover-only (no layout options)`);
        
        results.tests.push({
            name: 'Thrive image should be cover-only (no layout options)',
            status: test1Pass ? 'PASS' : 'FAIL',
            evidence: {
                hasRoomImage,
                hasSetupCards,
                setupKeywordsFound: foundKeywords,
                imageCount: chatImages.length,
                images: chatImages.map(img => ({ src: img.src, alt: img.alt })),
                assistantResponse: response3.text.substring(0, 400)
            }
        });
        
        console.log('\nStep 5: Continuing to AV summary confirmation...\n');
        console.log('User: "yes that looks good"');
        await sendMessage('yes that looks good');
        await page.waitForTimeout(2000);
        await page.screenshot({ path: 'screenshots/final-06-confirmed-look.png', fullPage: true });
        
        const response4 = await waitForAssistantResponse(assistantCount);
        console.log(`Assistant: "${response4.text.substring(0, 200)}..."\n`);
        assistantCount++;
        
        await page.waitForTimeout(3000);
        await page.screenshot({ path: 'screenshots/final-07-after-confirm.png', fullPage: true });
        
        console.log('Step 6: Checking for setup style follow-up question...\n');
        
        const response4Text = response4.text.toLowerCase();
        const setupQuestionKeywords = [
            'setup style',
            'how would you like',
            'theatre or cabaret',
            'choose a setup',
            'select a layout',
            'which setup',
            'what layout'
        ];
        
        const foundQuestionKeywords = setupQuestionKeywords.filter(k => response4Text.includes(k));
        const asksSetupStyle = foundQuestionKeywords.length > 0;
        
        console.log(`Setup style question keywords found: ${foundQuestionKeywords.length > 0 ? foundQuestionKeywords.join(', ') : 'None'}`);
        console.log(`Asks about setup style: ${asksSetupStyle}`);
        
        const test2Pass = !asksSetupStyle;
        console.log(`\n✓ TEST 2: ${test2Pass ? 'PASS' : 'FAIL'} - No setup-style follow-up question for Thrive`);
        
        results.tests.push({
            name: 'No setup-style follow-up question for Thrive',
            status: test2Pass ? 'PASS' : 'FAIL',
            evidence: {
                asksSetupStyle,
                setupQuestionKeywordsFound: foundQuestionKeywords,
                assistantResponse: response4.text.substring(0, 400)
            }
        });

        console.log('\nStep 7: Checking duplicate laptop prompts and question order...\n');

        const allMessages = await getChatMessages();
        const assistantTexts = allMessages
            .filter(m => m.type === 'assistant')
            .map(m => (m.text || '').toLowerCase());

        const ownershipPatterns = [
            'are you bringing your own laptop or do you need one',
            'bring your own laptop or do you need one'
        ];
        const preferencePatterns = [
            'windows or mac',
            'would you prefer windows or mac'
        ];
        const speakerStylePatterns = [
            'inbuilt speaker system',
            'external/portable pa speakers'
        ];

        const ownershipQuestionIndexes = assistantTexts
            .map((text, index) => ({ index, match: containsAny(text, ownershipPatterns) }))
            .filter(x => x.match)
            .map(x => x.index);

        const preferenceQuestionIndexes = assistantTexts
            .map((text, index) => ({ index, match: containsAny(text, preferencePatterns) }))
            .filter(x => x.match)
            .map(x => x.index);

        const speakerStyleQuestionIndex = assistantTexts.findIndex(text => containsAny(text, speakerStylePatterns));
        const firstLaptopQuestionIndex = ownershipQuestionIndexes.length > 0
            ? ownershipQuestionIndexes[0]
            : preferenceQuestionIndexes.length > 0
                ? preferenceQuestionIndexes[0]
                : -1;

        const laptopQuestionsAskedOnce = ownershipQuestionIndexes.length <= 1 && preferenceQuestionIndexes.length <= 1;
        const orderValid = speakerStyleQuestionIndex === -1 || firstLaptopQuestionIndex === -1 || firstLaptopQuestionIndex > speakerStyleQuestionIndex;
        const test3Pass = laptopQuestionsAskedOnce && orderValid;

        console.log(`Ownership laptop question count: ${ownershipQuestionIndexes.length}`);
        console.log(`Windows/Mac question count: ${preferenceQuestionIndexes.length}`);
        console.log(`Speaker-style index: ${speakerStyleQuestionIndex}`);
        console.log(`First laptop-question index: ${firstLaptopQuestionIndex}`);
        console.log(`\n✓ TEST 3: ${test3Pass ? 'PASS' : 'FAIL'} - Laptop prompts not repeated and ordering remains valid`);

        results.tests.push({
            name: 'Laptop prompts not repeated and ordering remains valid',
            status: test3Pass ? 'PASS' : 'FAIL',
            evidence: {
                ownershipQuestionCount: ownershipQuestionIndexes.length,
                preferenceQuestionCount: preferenceQuestionIndexes.length,
                speakerStyleQuestionIndex,
                firstLaptopQuestionIndex,
                laptopQuestionsAskedOnce,
                orderValid
            }
        });
        
        console.log('\n=== FINAL RESULTS ===\n');
        results.tests.forEach(test => {
            console.log(`${test.status}: ${test.name}`);
        });
        
        const allPassed = results.tests.every(t => t.status === 'PASS');
        console.log(`\nOVERALL: ${allPassed ? 'PASS ✓' : 'FAIL ✗'}\n`);
        
        // Save detailed results
        fs.writeFileSync('smoke-test-results.json', JSON.stringify(results, null, 2));
        console.log('Detailed results saved to smoke-test-results.json');
        console.log('Screenshots saved to screenshots/ directory');
        
    } catch (error) {
        console.error('\nERROR during test:', error.message);
        results.error = error.message;
        await page.screenshot({ path: 'screenshots/final-error.png', fullPage: true });
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
    const allPassed = results.tests.every(t => t.status === 'PASS');
    process.exit(allPassed ? 0 : 1);
}).catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
