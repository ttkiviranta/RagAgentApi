// Debug logging helper
window.debugLog = {
    log: function(message) {
        console.log('[Blazor] ' + message);
    },
    error: function(message) {
        console.error('[Blazor Error] ' + message);
    }
};
