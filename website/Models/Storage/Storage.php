<?php

namespace VoicesOfWynn\Models\Storage;

/**
 * Singleton accessor for storage instance.
 * Use Storage::get() to obtain the configured storage implementation.
 */
class Storage {
    private static ?StorageInterface $instance = null;

    /**
     * Get the storage instance (creates on first call)
     */
    public static function get(): StorageInterface {
        if (self::$instance === null) {
            self::$instance = self::create();
        }
        return self::$instance;
    }

    /**
     * Create storage instance based on environment config
     */
    private static function create(): StorageInterface {
        $type = getenv('STORAGE_TYPE') ?: 'local';

        if ($type === 'azure') {
            $connectionString = getenv('AZURE_STORAGE_CONNECTION_STRING');
            if (empty($connectionString)) {
                error_log('AZURE_STORAGE_CONNECTION_STRING not set, falling back to local storage');
                return new LocalStorage('dynamic/');
            }
            return new AzureBlobStorage($connectionString, 'vow-dynamic');
        }

        return new LocalStorage('dynamic/');
    }

    /**
     * Reset instance (for testing)
     */
    public static function reset(): void {
        self::$instance = null;
    }

    /**
     * Set a specific instance (for testing)
     */
    public static function setInstance(StorageInterface $storage): void {
        self::$instance = $storage;
    }
}
