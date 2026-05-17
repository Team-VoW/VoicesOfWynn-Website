// ==========================================================================
// SHARED CUSTOM AUDIO PLAYER
// ==========================================================================

(function () {
    // Single active audio playback — controls existing <audio> elements, only one plays at a time.
    let currentAudio = null;
    let currentButton = null;
    let onEnded = null;
    let onError = null;

    // Progressive functionality: Hide native controls so custom UI takes over
    // If JS is active, we assume the custom player UI (button + waveform) handles interaction.
    const audioElements = document.querySelectorAll('.custom-audio-player .audio-element');
    audioElements.forEach(audio => {
        audio.controls = false;
        audio.style.display = 'none'; // visually hide
    });

    // Handle play button clicks
    document.addEventListener('click', function (e) {
        const playBtn = e.target.closest('.audio-play-btn');
        if (!playBtn) return;

        const playerContainer = playBtn.closest('.custom-audio-player');
        if (!playerContainer) return;

        const audioEl = playerContainer.querySelector('.audio-element');

        // Validate audio element
        if (!audioEl) {
            console.error('No audio element found for button:', playBtn);
            return;
        }

        // If clicking the same button that's playing/paused, toggle it
        if (currentButton === playBtn && currentAudio === audioEl) {
            if (currentAudio.paused) {
                currentAudio.play().then(() => {
                    setButtonState(playBtn, true);
                }).catch(err => {
                    console.error('Playback failed:', err);
                    cleanupCurrentAudio();
                });
            } else {
                currentAudio.pause();
                setButtonState(playBtn, false);
            }
            return;
        }

        // Stop and properly clean up any currently playing audio
        if (currentAudio) {
            cleanupCurrentAudio();
        }

        // Set new audio state
        currentAudio = audioEl;
        currentButton = playBtn;

        // Store reference for callbacks
        const audioRef = currentAudio;

        onEnded = function () {
            if (audioRef === currentAudio) {
                setButtonState(playBtn, false);
                currentButton = null;
                currentAudio = null;
            }
            audioRef.removeEventListener('ended', onEnded);
        };

        onError = function () {
            if (audioRef === currentAudio) {
                setButtonState(playBtn, false);
                currentButton = null;
                currentAudio = null;
                console.error('Error playing audio');
            }
            audioRef.removeEventListener('error', onError);
        };

        currentAudio.addEventListener('ended', onEnded);
        currentAudio.addEventListener('error', onError);

        currentAudio.play().then(() => {
            setButtonState(playBtn, true);
        }).catch(err => {
            console.error('Playback failed:', err);
            cleanupCurrentAudio();
        });
    });

    function setButtonState(button, isPlaying) {
        if (isPlaying) {
            button.classList.add('playing');
            button.setAttribute('aria-label', 'Pause audio');
            button.setAttribute('aria-pressed', 'true');
        } else {
            button.classList.remove('playing');
            button.setAttribute('aria-label', 'Play audio');
            button.setAttribute('aria-pressed', 'false');
        }
    }

    function cleanupCurrentAudio() {
        if (!currentAudio) return;

        if (onEnded) currentAudio.removeEventListener('ended', onEnded);
        if (onError) currentAudio.removeEventListener('error', onError);

        currentAudio.pause();
        currentAudio.currentTime = 0; // Reset position

        if (currentButton) {
            setButtonState(currentButton, false);
        }

        currentAudio = null;
        currentButton = null;
        onEnded = null;
        onError = null;
    }
})();
