// ==========================================================================
// SHARED CUSTOM AUDIO PLAYER
// ==========================================================================

(function() {
    // Single shared audio element for all play buttons
    let currentAudio = null;
    let currentButton = null;

    // Handle play button clicks
    document.addEventListener('click', function(e) {
        const playBtn = e.target.closest('.audio-play-btn');
        if (!playBtn) return;

        const audioSrc = playBtn.dataset.src;

        // If clicking the same button that's playing, toggle pause/play
        if (currentButton === playBtn && currentAudio) {
            if (currentAudio.paused) {
                currentAudio.play();
                playBtn.classList.add('playing');
            } else {
                currentAudio.pause();
                playBtn.classList.remove('playing');
            }
            return;
        }

        // Stop any currently playing audio
        if (currentAudio) {
            currentAudio.pause();
            currentAudio.currentTime = 0;
            if (currentButton) {
                currentButton.classList.remove('playing');
            }
        }

        // Create new audio and play
        currentAudio = new Audio(audioSrc);
        currentButton = playBtn;

        currentAudio.addEventListener('ended', function() {
            playBtn.classList.remove('playing');
            currentButton = null;
        });

        currentAudio.addEventListener('error', function() {
            playBtn.classList.remove('playing');
            currentButton = null;
            console.error('Error loading audio:', audioSrc);
        });

        currentAudio.play().then(() => {
            playBtn.classList.add('playing');
        }).catch(err => {
            console.error('Playback failed:', err);
        });
    });
})();
