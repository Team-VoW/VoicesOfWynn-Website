<?php

namespace VoicesOfWynn\Models\Storage;

/**
 * Helper functions for use in views.
 * These are globally available after autoloading.
 */
function storageUrl(string $path, bool $cacheBust = false): string {
    return Storage::get()->getUrl($path, $cacheBust);
}

function storageBaseUrl(): string {
    return Storage::get()->getBaseUrl();
}
