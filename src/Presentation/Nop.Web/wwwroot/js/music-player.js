/* Music Player Logic */
document.addEventListener('DOMContentLoaded', () => {
    const audio = document.getElementById('homepage-audio');
    const playBtn = document.getElementById('play-pause-btn');
    const playerContainer = document.querySelector('.music-player-container');
    const playIcon = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';
    const pauseIcon = '<svg viewBox="0 0 24 24"><path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/></svg>';

    if (!audio || !playBtn) return;

    playBtn.addEventListener('click', () => {
        if (audio.paused) {
            audio.play().then(() => {
                playBtn.innerHTML = pauseIcon;
                playerContainer.classList.add('playing');
            }).catch(e => {
                console.error("Audio playback failed:", e);
                // Handle autoplay restrictions
                alert("Please click again to play music.");
            });
        } else {
            audio.pause();
            playBtn.innerHTML = playIcon;
            playerContainer.classList.remove('playing');
        }
    });

    // Optional: Visualizer state
    audio.addEventListener('ended', () => {
        playBtn.innerHTML = playIcon;
        playerContainer.classList.remove('playing');
    });
});
