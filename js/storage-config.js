// Storage configuration singleton
window.VoWStorage = (function() {
    // Config is now embedded in the HTML by PHP
    const config = window.STORAGE_CONFIG || {
        // Fallback if STORAGE_CONFIG is not defined
        baseUrl: 'dynamic/',
        paths: {
            recordings: 'recordings/',
            avatars: 'avatars/',
            npcs: 'npcs/',
            discordAvatars: 'discord-avatars/'
        }
    };

    return {
        // Get config (returns config directly, kept async for backward compatibility)
        getConfig: function() {
            return Promise.resolve(config);
        },

        // Build URL for a path
        getUrl: function(type, filename) {
            return Promise.resolve(config.baseUrl + config.paths[type] + filename);
        },

        // Synchronous URL builder
        getUrlSync: function(type, filename) {
            return config.baseUrl + config.paths[type] + filename;
        }
    };
})();
