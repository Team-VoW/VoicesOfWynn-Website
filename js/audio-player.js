// ==========================================================================
// SHARED CUSTOM AUDIO PLAYER
// ==========================================================================

(function () {
    // Single shared audio element for all play buttons
    let currentAudio = null;
    let currentButton = null;
    let onEnded = null;
    let onError = null;

    // Handle play button clicks
    document.addEventListener('click', function (e) {
        const playBtn = e.target.closest('.audio-play-btn');
        if (!playBtn) return;

        const audioSrc = playBtn.dataset.src;

        // Validate audio source
        if (!audioSrc) {
            console.error('No audio source found for button:', playBtn);
            return;
        }

        // If clicking the same button that's playing/paused, toggle it
        if (currentButton === playBtn && currentAudio) {
            if (currentAudio.paused) {
                currentAudio.play().then(() => {
                    playBtn.classList.add('playing');
                    playBtn.setAttribute('aria-label', 'Pause audio');
                }).catch(err => {
                    console.error('Playback failed:', err);
                    playBtn.classList.remove('playing');
                    playBtn.setAttribute('aria-label', 'Play audio');
                    currentAudio = null;
                    currentButton = null;
                });
            } else {
                currentAudio.pause();
                playBtn.classList.remove('playing');
                playBtn.setAttribute('aria-label', 'Play audio');
            }
            return;
        }

        // Stop and properly clean up any currently playing audio
        if (currentAudio) {
            // Remove event listeners using stored handler references
            if (onEnded) {
                currentAudio.removeEventListener('ended', onEnded);
            }
            if (onError) {
                currentAudio.removeEventListener('error', onError);
            }

            // Pause and reset the audio
            currentAudio.pause();
            currentAudio.currentTime = 0;

            // Clear the source to release resources
            currentAudio.src = '';

            // Remove the 'playing' class from the previous button
            if (currentButton) {
                currentButton.classList.remove('playing');
                currentButton.setAttribute('aria-label', 'Play audio');
            }

            // Null out references
            currentAudio = null;
            currentButton = null;
        }

        // Create new audio and play
        currentAudio = new Audio(audioSrc);
        currentButton = playBtn;

        // Store a local reference to this audio instance for validation in callbacks
        const audioRef = currentAudio;

        // Store event handler references for later cleanup
        onEnded = function (event) {
            // Only clear state if this audio is still the current one
            if (audioRef === currentAudio) {
                playBtn.classList.remove('playing');
                playBtn.setAttribute('aria-label', 'Play audio');
                currentButton = null;
                currentAudio = null;
            }
            // Remove listener after handling to prevent duplicate calls
            audioRef.removeEventListener('ended', onEnded);
        };

        onError = function (event) {
            // Only clear state if this audio is still the current one
            if (audioRef === currentAudio) {
                playBtn.classList.remove('playing');
                playBtn.setAttribute('aria-label', 'Play audio');
                currentButton = null;
                currentAudio = null;
                console.error('Error loading audio:', audioSrc);
            }
            // Remove listener after handling to prevent duplicate calls
            audioRef.removeEventListener('error', onError);
        };

        currentAudio.addEventListener('ended', onEnded);
        currentAudio.addEventListener('error', onError);

        currentAudio.play().then(() => {
            playBtn.classList.add('playing');
            playBtn.setAttribute('aria-label', 'Pause audio');
        }).catch(err => {
            console.error('Playback failed:', err);
            playBtn.classList.remove('playing');
            playBtn.setAttribute('aria-label', 'Play audio');
            currentButton = null;
            currentAudio = null;
        });
    });
})();
