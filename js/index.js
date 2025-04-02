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
