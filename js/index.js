// JavaScript function to load a YouTube video into a container
function loadVideo(container, videoId) {
  const iframe = document.createElement("iframe");
  iframe.src = "https://www.youtube.com/embed/" + videoId + "?autoplay=1";
  iframe.frameBorder = "0";
  iframe.allow =
    "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture";
  iframe.allowFullscreen = true;

  container.innerHTML = "";
  container.appendChild(iframe);
}

// Intersection Observer for scroll animations
document.addEventListener('DOMContentLoaded', function () {
  const animatedElements = document.querySelectorAll('.animate-on-scroll');

  // Check if user prefers reduced motion
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  if (prefersReducedMotion) {
    // If user prefers reduced motion, just show all elements immediately
    animatedElements.forEach(el => {
      el.classList.add('is-visible');
    });
  } else {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        }
      });
    }, {
      threshold: 0.1,
      rootMargin: '0px 0px -50px 0px'
    });

    animatedElements.forEach(el => observer.observe(el));
  }

  // Video container event listeners (CSP-compliant)
  const videoContainers = document.querySelectorAll('.video-container');

  videoContainers.forEach(container => {
    // Click event
    container.addEventListener('click', function () {
      const videoId = this.dataset.videoId;
      if (videoId) {
        loadVideo(this, videoId);
      }
    });

    // Keyboard event (Enter or Space)
    container.addEventListener('keydown', function (event) {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        const videoId = this.dataset.videoId;
        if (videoId) {
          loadVideo(this, videoId);
        }
      }
    });
  });
});
