// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Varje ordkort spelar en lokal MP3-fil. Utan JavaScript finns vanliga ljudkontroller.
(() => {
    const players = Array.from(document.querySelectorAll("[data-word-audio]"));
    let latestRequest = 0;

    for (const player of players) {
        const audio = player.querySelector("audio");
        const actions = player.querySelector(".audio-actions");
        const status = player.querySelector("[data-audio-status]");
        if (!audio || !actions || !status) continue;
        audio.controls = false;
        audio.hidden = true;
        actions.hidden = false;

        audio.addEventListener("ended", () => {
            status.textContent = "Säg ordet högt och lyssna igen.";
        });
        audio.addEventListener("error", () => {
            status.textContent = "Ljudet kunde inte laddas. Försök igen.";
        });

        for (const button of actions.querySelectorAll("[data-audio-rate]")) {
            button.addEventListener("click", async () => {
                const request = ++latestRequest;
                // Stoppa andra ord så att inspelningarna inte överlappar.
                for (const other of players) {
                    other.querySelector("audio")?.pause();
                    const otherStatus = other.querySelector("[data-audio-status]");
                    if (otherStatus) otherStatus.textContent = "";
                }
                audio.playbackRate = Number(button.dataset.audioRate);
                audio.preservesPitch = true;
                audio.currentTime = 0;
                status.textContent = "Laddar ljud…";
                try {
                    await audio.play();
                    if (request !== latestRequest) return;
                    status.textContent = audio.playbackRate < 1 ? "Spelar långsamt…" : "Spelar…";
                } catch (error) {
                    if (request !== latestRequest) return;
                    status.textContent = "Ljudet kunde inte spelas. Försök igen.";
                }
            });
        }
    }
})();
