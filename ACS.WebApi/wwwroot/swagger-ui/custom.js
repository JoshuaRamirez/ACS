/* Custom JavaScript for ACS API Swagger UI */

// Add custom functionality when the page loads
document.addEventListener('DOMContentLoaded', function() {
    // Add version info to header
    setTimeout(function() {
        addVersionInfo();
        addQuickStartGuide();
        enhanceAuthorizationFlow();
        addResponseTimeDisplay();
        addEnvironmentSelector();
    }, 1000);
});

// Add version and build information
function addVersionInfo() {
    const infoSection = document.querySelector('.swagger-ui .info');
    if (infoSection && !document.querySelector('.custom-version-info')) {
        const versionInfo = document.createElement('div');
        versionInfo.className = 'custom-version-info api-info-box';
        versionInfo.innerHTML = `
            <h4>🚀 API Version Information</h4>
            <p><strong>Version:</strong> v1.0 | <strong>Build:</strong> ${new Date().toLocaleDateString()} | <strong>Environment:</strong> ${getEnvironment()}</p>
            <p><strong>Rate Limits:</strong> Authentication endpoints: 5 req/5min | Standard endpoints: 100 req/min | Search endpoints: 1000 req/min</p>
        `;
        infoSection.appendChild(versionInfo);
    }
}

// Add quick start guide
function addQuickStartGuide() {
    const infoSection = document.querySelector('.swagger-ui .info');
    if (infoSection && !document.querySelector('.quick-start-guide')) {
        const quickStart = document.createElement('div');
        quickStart.className = 'quick-start-guide api-info-box';
        quickStart.innerHTML = `
            <h4>⚡ Quick Start Guide</h4>
            <ol>
                <li><strong>Authenticate:</strong> Use <code>POST /api/auth/login</code> with credentials: <code>{"username": "admin", "password": "admin123"}</code></li>
                <li><strong>Authorize:</strong> Copy the token from response and click the 🔒 "Authorize" button above</li>
                <li><strong>Explore:</strong> Try endpoints like <code>GET /api/users</code> or <code>GET /api/groups</code></li>
                <li><strong>Create Resources:</strong> Use POST endpoints to create users, groups, and roles</li>
            </ol>
            <p><strong>💡 Tip:</strong> Use the "Try it out" button on any endpoint to test with your token!</p>
        `;
        infoSection.appendChild(quickStart);
    }
}

// Enhance authorization flow with better UX
function enhanceAuthorizationFlow() {
    // Monitor for authorization modal
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.type === 'childList') {
                const authModal = document.querySelector('.swagger-ui .auth-wrapper');
                if (authModal && !authModal.querySelector('.custom-auth-help')) {
                    addAuthHelp(authModal);
                }
            }
        });
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
}

// Add helpful text to authorization modal
function addAuthHelp(authModal) {
    const helpDiv = document.createElement('div');
    helpDiv.className = 'custom-auth-help api-warning-box';
    helpDiv.innerHTML = `
        <h4>🔐 JWT Token Authorization</h4>
        <p>Enter your JWT token below (obtained from the login endpoint):</p>
        <ol>
            <li>First, call <code>POST /api/auth/login</code> with your credentials</li>
            <li>Copy the 'token' value from the response</li>
            <li>Paste it in the value field below (don't include 'Bearer')</li>
            <li>Click "Authorize" to apply to all requests</li>
        </ol>
        <p><strong>Note:</strong> Tokens expire after 24 hours in development, 8 hours in production.</p>
    `;
    
    const authContainer = authModal.querySelector('.auth-container');
    if (authContainer && !authContainer.querySelector('.custom-auth-help')) {
        authContainer.insertBefore(helpDiv, authContainer.firstChild);
    }
}

// Add response time display
function addResponseTimeDisplay() {
    // Hook into fetch requests to measure response time
    const originalFetch = window.fetch;
    window.fetch = function(...args) {
        const startTime = performance.now();
        
        return originalFetch.apply(this, args).then(response => {
            const endTime = performance.now();
            const responseTime = Math.round(endTime - startTime);
            
            // Add response time to response display
            setTimeout(() => {
                const responseBody = document.querySelector('.swagger-ui .responses-wrapper .response .response-col_description');
                if (responseBody && !responseBody.querySelector('.response-time-info')) {
                    const timeInfo = document.createElement('div');
                    timeInfo.className = 'response-time-info api-info-box';
                    timeInfo.innerHTML = `⏱️ <strong>Response Time:</strong> ${responseTime}ms`;
                    responseBody.appendChild(timeInfo);
                }
            }, 100);
            
            return response;
        });
    };
}

// Add environment selector
function addEnvironmentSelector() {
    const serversWrapper = document.querySelector('.swagger-ui .servers-wrapper');
    if (serversWrapper && !document.querySelector('.custom-env-info')) {
        const envInfo = document.createElement('div');
        envInfo.className = 'custom-env-info api-warning-box';
        envInfo.innerHTML = `
            <h4>🌍 Environment Selection</h4>
            <p>Choose your target environment from the server dropdown above:</p>
            <ul>
                <li><strong>Production:</strong> api.acs.com - Live production data</li>
                <li><strong>Staging:</strong> staging-api.acs.com - Pre-production testing</li>
                <li><strong>Development:</strong> localhost - Local development server</li>
            </ul>
            <p><strong>⚠️ Warning:</strong> Be careful when using production environment!</p>
        `;
        serversWrapper.parentNode.insertBefore(envInfo, serversWrapper.nextSibling);
    }
}

// Utility function to detect environment
function getEnvironment() {
    const hostname = window.location.hostname;
    if (hostname === 'localhost' || hostname === '127.0.0.1') {
        return 'Development';
    } else if (hostname.includes('staging')) {
        return 'Staging';
    } else {
        return 'Production';
    }
}

// Add custom swagger plugin
const ACSPlugin = function() {
    return {
        components: {
            // Custom component for operation summaries
            OperationSummary: function(props) {
                const { operation } = props;
                const customSummary = operation.get('summary') || 'API Operation';
                return React.createElement('div', {
                    className: 'custom-operation-summary'
                }, customSummary);
            }
        }
    };
};

// Auto-expand authentication section on page load
setTimeout(function() {
    const authSection = document.querySelector('.swagger-ui .opblock-tag-section[data-tag="Authentication"]');
    if (authSection) {
        const authButton = authSection.querySelector('.opblock-tag');
        if (authButton && !authSection.classList.contains('is-open')) {
            authButton.click();
        }
    }
}, 1500);

// Add keyboard shortcuts
document.addEventListener('keydown', function(e) {
    // Ctrl+/ or Cmd+/ to focus search
    if ((e.ctrlKey || e.metaKey) && e.key === '/') {
        e.preventDefault();
        const searchInput = document.querySelector('.swagger-ui .filter-container input');
        if (searchInput) {
            searchInput.focus();
        }
    }
    
    // Escape to clear search
    if (e.key === 'Escape') {
        const searchInput = document.querySelector('.swagger-ui .filter-container input');
        if (searchInput && searchInput === document.activeElement) {
            searchInput.value = '';
            searchInput.dispatchEvent(new Event('input'));
        }
    }
});

// Add helpful tooltips
function addTooltips() {
    // Add tooltips to response codes
    const responseCodes = document.querySelectorAll('.swagger-ui .response-col_status');
    responseCodes.forEach(function(code) {
        const statusCode = code.textContent.trim();
        let tooltip = '';
        
        switch (statusCode) {
            case '200':
                tooltip = 'Success: Request processed successfully';
                break;
            case '201':
                tooltip = 'Created: Resource created successfully';
                break;
            case '400':
                tooltip = 'Bad Request: Invalid input or request format';
                break;
            case '401':
                tooltip = 'Unauthorized: Invalid or missing authentication';
                break;
            case '403':
                tooltip = 'Forbidden: Insufficient permissions';
                break;
            case '404':
                tooltip = 'Not Found: Resource does not exist';
                break;
            case '429':
                tooltip = 'Rate Limited: Too many requests';
                break;
            case '500':
                tooltip = 'Server Error: Internal server error occurred';
                break;
        }
        
        if (tooltip) {
            code.setAttribute('title', tooltip);
        }
    });
}

// Run tooltips after content loads
setTimeout(addTooltips, 2000);