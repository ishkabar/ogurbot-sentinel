(function() {

    // Helper do odczytu cookie
    function getCookie(name) {
        const cookies = document.cookie.split(';');
        for (let cookie of cookies) {
            const [cookieName, cookieValue] = cookie.trim().split('=');
            if (cookieName === name) {
                return cookieValue;
            }
        }
        return null;
    }

    const originalFetch = window.fetch;
    window.fetch = function(url, options = {}) {

        const token = getCookie('auth_token');

        // ✅ NIE dodawaj tokenu do endpointów auth (login, logout)
        const isAuthEndpoint = typeof url === 'string' && url.includes('/api/auth/');

        // Dla requestów do własnego API, wyślij tylko credentials (cookie)
        // NIE dodawaj Authorization header - middleware czyta z cookie!
        if (typeof url === 'string' && url.startsWith('/') && !isAuthEndpoint) {
            options.credentials = options.credentials || 'include';
        } else {
        }


        return originalFetch(url, options)
            .then(response => {

                if (response.status === 401 || response.status === 403) {
                    const currentPath = window.location.pathname.toLowerCase();

                    const publicPaths = ['/', '/login', '/privacy', '/download', '/index'];
                    if (!publicPaths.includes(currentPath)) {
                        // Clear cookie
                        document.cookie = 'auth_token=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;';
                        document.cookie = 'username=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;';
                        document.cookie = 'role=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;';
                        window.location.href = '/Login';
                    } else {
                    }
                }
                return response;
            })
            .catch(error => {
                console.error('💥 FETCH ERROR:', error);
                throw error;
            });
    };

})();