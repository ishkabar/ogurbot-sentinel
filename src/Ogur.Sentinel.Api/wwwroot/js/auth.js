(function() {
    console.log('🚀 AUTH.JS LOADED');

    // Helper do odczytu cookie
    function getCookie(name) {
        const cookies = document.cookie.split(';');
        console.log('🍪 ALL COOKIES:', document.cookie);
        for (let cookie of cookies) {
            const [cookieName, cookieValue] = cookie.trim().split('=');
            if (cookieName === name) {
                console.log(`✅ Found cookie ${name}:`, cookieValue.substring(0, 20) + '...');
                return cookieValue;
            }
        }
        console.log(`❌ Cookie ${name} NOT FOUND`);
        return null;
    }

    const originalFetch = window.fetch;
    window.fetch = function(url, options = {}) {
        console.log('🌐 FETCH INTERCEPTED:', url);

        const token = getCookie('auth_token');
        console.log('🔑 Token from cookie:', token ? token.substring(0, 20) + '... (length: ' + token.length + ')' : 'NULL');

        // ✅ NIE dodawaj tokenu do endpointów auth (login, logout)
        const isAuthEndpoint = typeof url === 'string' && url.includes('/api/auth/');
        console.log('🔐 Is auth endpoint?', isAuthEndpoint);

        // Dla requestów do własnego API, wyślij tylko credentials (cookie)
        // NIE dodawaj Authorization header - middleware czyta z cookie!
        if (typeof url === 'string' && url.startsWith('/') && !isAuthEndpoint) {
            console.log('✅ Adding credentials (cookie will be sent automatically)');
            options.credentials = options.credentials || 'include';
        } else {
            console.log('⏭️ Skipping credentials for this request');
        }

        console.log('📤 Final request options:', JSON.stringify(options, null, 2));

        return originalFetch(url, options)
            .then(response => {
                console.log(`📥 Response from ${url}:`, response.status, response.statusText);

                if (response.status === 401 || response.status === 403) {
                    console.log('🚫 UNAUTHORIZED/FORBIDDEN - checking if should redirect');
                    const currentPath = window.location.pathname.toLowerCase();
                    console.log('📍 Current path:', currentPath);

                    const publicPaths = ['/', '/login', '/privacy', '/download', '/index'];
                    if (!publicPaths.includes(currentPath)) {
                        console.log('❌ Not a public path - clearing cookies and redirecting');
                        // Clear cookie
                        document.cookie = 'auth_token=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;';
                        document.cookie = 'username=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;';
                        document.cookie = 'role=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;';
                        console.log('🔄 Redirecting to /Login');
                        window.location.href = '/Login';
                    } else {
                        console.log('✅ Public path - not redirecting');
                    }
                }
                return response;
            })
            .catch(error => {
                console.error('💥 FETCH ERROR:', error);
                throw error;
            });
    };

    console.log('✅ Fetch interceptor installed');
})();