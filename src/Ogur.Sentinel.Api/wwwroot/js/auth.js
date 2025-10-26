(function() {
    const originalFetch = window.fetch;
    window.fetch = function(url, options = {}) {
        const token = localStorage.getItem('auth_token');
        if (token && typeof url === 'string' && url.startsWith('/')) {
            options.headers = options.headers || {};
            if (!options.headers['Authorization']) {
                options.headers['Authorization'] = `Bearer ${token}`;
            }
        }
        return originalFetch(url, options)
            .then(response => {
                if (response.status === 401) {
                    const currentPath = window.location.pathname.toLowerCase();
                    const publicPaths = ['/', '/login', '/privacy', '/download', '/index'];
                    if (!publicPaths.includes(currentPath)) {
                        localStorage.removeItem('auth_token');
                        localStorage.removeItem('token_expires');
                        localStorage.removeItem('username');
                        window.location.href = '/Login';
                    }
                }
                return response;
            });
    };
})();