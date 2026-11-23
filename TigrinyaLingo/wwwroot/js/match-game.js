(() => {
    'use strict';
    const root = document.getElementById('match-game');
    if (!root || !window.fetch || !window.crypto?.randomUUID) return;
    const board = JSON.parse(document.getElementById('match-game-board').textContent);
    const form = document.getElementById('game-finish');
    const next = document.getElementById('game-next');
    const status = document.getElementById('game-status');
    const selected = { Swedish: null, Tigrinya: null };
    let round = 0, busy = false, locked = false, action = null, pending = null, activeAudio = null;
    const cards = [];
    function stopAudio() { if (activeAudio) { activeAudio.pause(); activeAudio.currentTime = 0; activeAudio = null; } }
    function feedback(message, label, callback) {
        status.textContent = message;
        next.hidden = !label;
        next.textContent = label || 'Fortsätt';
        action = callback;
    }
    function refresh() {
        for (const card of cards) {
            const chosen = selected[card.side] === card;
            const visible = !board.IsMemory || chosen || card.matched;
            card.button.replaceChildren();
            const text = document.createElement('span'); text.textContent = visible ? card.data.Text : '?';
            card.button.append(text);
            if (visible && card.data.UsageLabel) {
                const usage = document.createElement('small'); usage.className = 'phrase-choice-label'; usage.lang = 'sv';
                usage.textContent = card.data.UsageLabel; card.button.append(usage);
            }
            card.button.setAttribute('aria-label', visible ? card.data.Text + (card.data.UsageLabel ? ', ' + card.data.UsageLabel : '') + (card.matched ? ', matchat par' : '') : `Vänd ${card.side === 'Swedish' ? 'svenskt kort' : 'tigrinjakort'} ${card.number}`);
            card.button.setAttribute('aria-pressed', String(chosen));
            card.button.classList.toggle('selected', chosen);
            card.button.classList.toggle('matched', card.matched);
            card.button.classList.toggle('face-down', !visible);
            card.button.disabled = busy || locked || card.matched;
            if (card.audioButton) { card.audioButton.hidden = !visible; card.audioButton.disabled = busy; }
        }
        next.disabled = busy;
    }
    function clearSelection() {
        for (const card of cards) card.button.classList.remove('wrong');
        selected.Swedish = null; selected.Tigrinya = null; pending = null; locked = false;
        stopAudio(); refresh(); feedback(board.IsMemory ? 'Vänd ett kort i varje kolumn.' : 'Vilka uttryck hör ihop?');
        cards.find(card => !card.matched)?.button.focus();
    }
    function renderRound(focus) {
        stopAudio(); cards.length = 0; selected.Swedish = null; selected.Tigrinya = null; pending = null; locked = false;
        document.getElementById('game-round').textContent = `Runda ${round + 1} av ${board.Rounds.length}`;
        for (const side of ['Swedish', 'Tigrinya']) {
            const column = document.getElementById(side === 'Swedish' ? 'swedish-cards' : 'tigrinya-cards');
            column.replaceChildren();
            board.Rounds[round][side].forEach((data, index) => {
                const wrapper = document.createElement('div'); wrapper.className = 'game-tile-wrap';
                const button = document.createElement('button'); button.type = 'button'; button.className = 'game-tile';
                if (side === 'Tigrinya') button.lang = 'ti-Latn';
                const card = { data, side, button, number: index + 1, matched: false };
                cards.push(card); wrapper.append(button); column.append(wrapper);
                button.addEventListener('click', () => {
                    if (busy || locked || card.matched) return;
                    selected[side] = selected[side] === card ? null : card;
                    stopAudio(); refresh();
                    if (selected.Swedish && selected.Tigrinya) {
                        pending = { id: crypto.randomUUID(), left: selected.Swedish, right: selected.Tigrinya };
                        check();
                    } else feedback('Välj också ett kort i den andra kolumnen.');
                });
                if (side === 'Tigrinya' && data.AudioFileName) {
                    const speaker = document.createElement('button'); speaker.type = 'button'; speaker.className = 'game-listen';
                    speaker.textContent = '♪'; speaker.setAttribute('aria-label', `Lyssna på ${data.Text}`);
                    const audio = new Audio('/audio/tigrinya/' + encodeURIComponent(data.AudioFileName)); audio.preload = 'none';
                    speaker.addEventListener('click', async () => {
                        stopAudio(); activeAudio = audio;
                        try { await audio.play(); } catch { status.textContent = 'Inspelningen kunde inte spelas. Du kan fortsätta matcha orden.'; }
                    });
                    card.audioButton = speaker; wrapper.append(speaker);
                }
            });
        }
        refresh(); feedback(board.IsMemory ? 'Vänd ett kort i varje kolumn.' : 'Vilka uttryck hör ihop?');
        if (focus) cards[0].button.focus();
    }
    async function check() {
        busy = true; refresh(); feedback('Kontrollerar paret …');
        const body = new FormData(form);
        body.append('RequestId', pending.id); body.append('SwedishKey', pending.left.data.Key); body.append('TigrinyaKey', pending.right.data.Key);
        try {
            const response = await fetch(root.dataset.checkUrl, { method: 'POST', body, credentials: 'same-origin' });
            if (!response.ok || response.redirected) {
                if (response.status === 409 || response.status === 400 || response.redirected) {
                    locked = true;
                    feedback('Omgången är inte tillgänglig längre. Starta en ny omgång.', 'Starta om', () => window.location.reload());
                    return;
                }
                throw new Error('request');
            }
            const result = await response.json();
            locked = false;
            document.getElementById('game-points').textContent = result.points;
            document.getElementById('game-streak').textContent = `${result.streak} i följd`;
            document.getElementById('game-progress').value = result.matchedCount;
            document.getElementById('game-count').textContent = `${result.matchedCount} av ${result.totalPairs} par`;
            if (!result.isCorrect) {
                locked = true;
                pending.left.button.classList.add('wrong'); pending.right.button.classList.add('wrong');
                feedback('Inte samma betydelse. Titta på uttrycken och prova igen!', 'Prova igen', clearSelection);
            } else {
                pending.left.matched = true; pending.right.matched = true;
                selected.Swedish = null; selected.Tigrinya = null; pending = null;
                if (result.roundComplete) {
                    locked = true;
                    feedback(result.gameComplete ? 'Alla par matchade! Dags att se dina stjärnor.' : 'Snyggt! Du hittade alla par i rundan.',
                        result.gameComplete ? 'Visa resultat' : 'Nästa runda', () => {
                            if (result.gameComplete) { next.disabled = true; stopAudio(); form.requestSubmit(); }
                            else { round = result.nextRound; renderRound(true); }
                        });
                } else feedback(result.streak > 1 ? `Rätt par! ${result.streak} i följd – fortsätt så!` : 'Rätt par! Hitta nästa.');
            }
        } catch {
            locked = true;
            feedback('Kunde inte nå spelet. Kontrollera anslutningen och försök igen.', 'Försök igen', check);
        } finally {
            busy = false; refresh();
            if (locked) next.focus();
            else cards.find(card => !card.matched)?.button.focus();
        }
    }
    next.addEventListener('click', () => { if (!busy && action) action(); });
    window.addEventListener('pagehide', stopAudio);
    renderRound(false); root.hidden = false; document.getElementById('game-fallback').hidden = true;
})();
