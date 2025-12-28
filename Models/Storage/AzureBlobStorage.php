<?php

namespace VoicesOfWynn\Models\Storage;

use MicrosoftAzure\Storage\Blob\BlobRestProxy;
use MicrosoftAzure\Storage\Blob\Models\CreateBlockBlobOptions;
use MicrosoftAzure\Storage\Blob\Models\ListBlobsOptions;
use MicrosoftAzure\Storage\Common\Exceptions\ServiceException;

class AzureBlobStorage implements StorageInterface {
    private BlobRestProxy $client;
    private string $containerName;
    private string $baseUrl;

    // MIME type mapping
    private const MIME_TYPES = [
        'ogg' => 'audio/ogg',
        'jpg' => 'image/jpeg',
        'jpeg' => 'image/jpeg',
        'png' => 'image/png',
    ];

    public function __construct(string $connectionString, string $containerName) {
        $this->client = BlobRestProxy::createBlobService($connectionString);
        $this->containerName = $containerName;

        // Extract account name from connection string to build base URL
        preg_match('/AccountName=([^;]+)/', $connectionString, $matches);
        $accountName = $matches[1] ?? 'vowstorage';
        $this->baseUrl = "https://{$accountName}.blob.core.windows.net/{$containerName}/";
    }

    public function upload(string $sourcePath, string $destinationPath, ?string $contentType = null): bool {
        try {
            $content = file_get_contents($sourcePath);
            if ($content === false) {
                throw new StorageException("Cannot read source file", 'upload', $sourcePath);
            }

            $options = new CreateBlockBlobOptions();

            // Set content type
            if ($contentType === null) {
                $ext = strtolower(pathinfo($destinationPath, PATHINFO_EXTENSION));
                $contentType = self::MIME_TYPES[$ext] ?? 'application/octet-stream';
            }
            $options->setContentType($contentType);

            // Set cache control based on content type
            if (str_starts_with($contentType, 'image/')) {
                $options->setCacheControl('public, max-age=31536000'); // 1 year for images
            } else {
                $options->setCacheControl('public, max-age=3600'); // 1 hour for audio
            }

            $this->client->createBlockBlob($this->containerName, $destinationPath, $content, $options);
            return true;
        } catch (ServiceException $e) {
            throw new StorageException("Azure upload failed: " . $e->getMessage(), 'upload', $destinationPath, $e);
        }
    }

    public function delete(string $path): bool {
        try {
            $this->client->deleteBlob($this->containerName, $path);
            return true;
        } catch (ServiceException $e) {
            if ($e->getCode() === 404) {
                return true; // File doesn't exist = success
            }
            throw new StorageException("Azure delete failed: " . $e->getMessage(), 'delete', $path, $e);
        }
    }

    public function deleteByPrefix(string $prefix): array {
        try {
            $options = new ListBlobsOptions();
            $options->setPrefix($prefix);

            $deleted = [];
            $result = $this->client->listBlobs($this->containerName, $options);

            foreach ($result->getBlobs() as $blob) {
                $blobName = $blob->getName();
                $this->client->deleteBlob($this->containerName, $blobName);
                $deleted[] = $blobName;
            }
            return $deleted;
        } catch (ServiceException $e) {
            throw new StorageException("Azure deleteByPrefix failed: " . $e->getMessage(), 'deleteByPrefix', $prefix, $e);
        }
    }

    public function rename(string $oldPath, string $newPath): bool {
        try {
            // Azure doesn't support rename - must copy then delete
            $this->client->copyBlob($this->containerName, $newPath, $this->containerName, $oldPath);

            // Wait for the copy to complete before deleting
            $this->waitForCopyCompletion($newPath);

            $this->client->deleteBlob($this->containerName, $oldPath);
            return true;
        } catch (ServiceException $e) {
            throw new StorageException("Azure rename failed: " . $e->getMessage(), 'rename', $oldPath, $e);
        }
    }

    public function copy(string $sourcePath, string $destinationPath): bool {
        try {
            $this->client->copyBlob($this->containerName, $destinationPath, $this->containerName, $sourcePath);
            return true;
        } catch (ServiceException $e) {
            throw new StorageException("Azure copy failed: " . $e->getMessage(), 'copy', $sourcePath, $e);
        }
    }

    public function exists(string $path): bool {
        try {
            $this->client->getBlobMetadata($this->containerName, $path);
            return true;
        } catch (ServiceException $e) {
            if ($e->getCode() === 404) {
                return false;
            }
            throw new StorageException("Azure exists check failed: " . $e->getMessage(), 'exists', $path, $e);
        }
    }

    public function getUrl(string $path, bool $cacheBust = false): string {
        // Encode each path segment to handle special characters while preserving directory separators
        $segments = explode('/', $path);
        $encodedSegments = array_map('rawurlencode', $segments);
        $encodedPath = implode('/', $encodedSegments);

        $url = $this->baseUrl . $encodedPath;
        if ($cacheBust) {
            $url .= '?v=' . time();
        }
        return $url;
    }

    public function getBaseUrl(): string {
        return $this->baseUrl;
    }

    /**
     * Waits for an asynchronous blob copy operation to complete.
     * Polls the blob properties until the copy status is 'success' or fails.
     *
     * @param string $blobPath Path to the destination blob being copied
     * @throws StorageException If copy fails or times out
     */
    private function waitForCopyCompletion(string $blobPath): void {
        $maxAttempts = 60; // Maximum 60 attempts
        $sleepSeconds = 1; // Wait 1 second between polls

        for ($i = 0; $i < $maxAttempts; $i++) {
            try {
                $properties = $this->client->getBlobProperties($this->containerName, $blobPath);
                $copyStatus = $properties->getProperties()->getCopyState();

                if ($copyStatus === null) {
                    // No copy operation found - might be a same-container instant copy
                    return;
                }

                $status = $copyStatus->getStatus();

                if ($status === 'success') {
                    return;
                }

                if ($status === 'failed' || $status === 'aborted') {
                    throw new StorageException(
                        "Blob copy failed with status: {$status}",
                        'waitForCopyCompletion',
                        $blobPath
                    );
                }

                // Status is 'pending' - wait and retry
                sleep($sleepSeconds);

            } catch (ServiceException $e) {
                throw new StorageException(
                    "Failed to check copy status: " . $e->getMessage(),
                    'waitForCopyCompletion',
                    $blobPath,
                    $e
                );
            }
        }

        throw new StorageException(
            "Blob copy timed out after {$maxAttempts} seconds",
            'waitForCopyCompletion',
            $blobPath
        );
    }
}
