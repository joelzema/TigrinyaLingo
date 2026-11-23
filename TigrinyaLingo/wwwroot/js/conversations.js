(() => {
    'use strict';
    const root = document.getElementById('conversation-game');
    if (!root || !window.fetch || !window.crypto?.randomUUID) return;
    const board = JSON.parse(document.getElementById('conversation-board').textContent);
    const form = document.getElementById('conversation-finish');
    const log = document.getElementById('conversation-log');
    const choices = document.getElementById('conversation-choices');
    const check = document.getElementById('conversation-check');
    const next = document.getElementById('conversation-next');
    const feedback = document.getElementById('conversation-feedback');
    let index = 0, selected = null, pending = null, busy = false, answered = false, restart = false;
    let buttons = [];
    const stepLabel = root.dataset.stepLabel || 'Replik';
    const pointsLabel = root.dataset.pointsLabel || 'samtalspoäng';
    function refresh() {
        for (const option of buttons) {
            option.button.disabled = busy || answered || pending !== null;
            option.button.setAttribute('aria-pressed', String(selected?.Key === option.choice.Key));
            option.button.classList.toggle('selected', selected?.Key === option.choice.Key);
        }
        check.disabled = busy || (!restart && (!selected || answered));
        next.disabled = busy;
    }
    function renderTurn(focus) {
        selected = null; pending = null; answered = false; restart = false;
        const turn = board.Turns[index];
        document.getElementById('conversation-step').textContent = `${stepLabel} ${index + 1} av ${board.Turns.length}`;
        const section = document.createElement('section'); section.className = 'conversation-turn';
        const context = document.createElement('p'); context.className = 'conversation-situation'; context.textContent = turn.Situation;
        const bubble = document.createElement('div'); bubble.className = 'conversation-bubble partner'; bubble.tabIndex = -1;
        const name = document.createElement('span'); name.className = 'conversation-speaker'; name.textContent = root.dataset.partnerLabel || 'Din samtalspartner · svenskt lässtöd';
        const line = document.createElement('p'); line.textContent = turn.PartnerLine;
        bubble.append(name, line); section.append(context, bubble); log.append(section);
        choices.replaceChildren(); buttons = [];
        turn.Choices.forEach((choice, optionIndex) => {
            const button = document.createElement('button'); button.type = 'button'; button.className = 'conversation-option';
            const number = document.createElement('span'); number.className = 'conversation-option-number'; number.textContent = optionIndex + 1; number.setAttribute('aria-hidden', 'true');
            const text = document.createElement('span'); text.lang = 'ti-Latn'; text.textContent = choice.Text;
            const wording = document.createElement('span'); wording.append(text);
            if (choice.UsageLabel) { const usage = document.createElement('small'); usage.className = 'phrase-choice-label'; usage.lang = 'sv'; usage.textContent = choice.UsageLabel; wording.append(usage); }
            button.append(number, wording); button.setAttribute('aria-label', choice.Text + (choice.UsageLabel ? ', ' + choice.UsageLabel : ''));
            button.addEventListener('click', () => {
                if (busy || answered || pending) return;
                selected = choice; refresh();
            });
            choices.append(button); buttons.push({button, choice});
        });
        feedback.textContent = ''; feedback.className = 'conversation-feedback';
        check.hidden = false; check.textContent = 'Kontrollera svaret'; next.hidden = true;
        refresh(); if (focus) bubble.focus();
    }
    async function sendAnswer() {
        if (restart) { window.location.reload(); return; }
        if (busy || !selected || answered) return;
        pending ??= { requestId: crypto.randomUUID(), turn: board.Turns[index], choice: selected };
        busy = true; refresh(); feedback.textContent = 'Kontrollerar svaret …';
        const body = new FormData(form);
        body.append('RequestId', pending.requestId); body.append('TurnKey', pending.turn.Key); body.append('ChoiceKey', pending.choice.Key);
        try {
            const response = await fetch(root.dataset.checkUrl, {method: 'POST', body, credentials: 'same-origin'});
            if (!response.ok || response.redirected) {
                if (response.status === 400 || response.status === 409 || response.redirected) {
                    restart = true; feedback.textContent = 'Omgången är inte tillgänglig längre. Starta ett nytt samtal.';
                    check.textContent = 'Starta om samtalet'; return;
                }
                throw new Error('request');
            }
            const result = await response.json();
            answered = true;
            document.getElementById('conversation-points').textContent = `${result.points} ${pointsLabel}`;
            document.getElementById('conversation-progress').value = result.answeredCount;
            for (const option of buttons) {
                option.button.classList.toggle('correct', option.choice.Key === result.correctChoiceKey);
                option.button.classList.toggle('wrong', !result.isCorrect && option.choice.Key === selected.Key);
            }
            const reply = document.createElement('div'); reply.className = 'conversation-bubble learner';
            const name = document.createElement('span'); name.className = 'conversation-speaker'; name.textContent = 'Du';
            const text = document.createElement('p'); text.lang = 'ti-Latn'; text.textContent = selected.Text;
            reply.append(name, text);
            if (selected.UsageLabel) { const usage = document.createElement('small'); usage.lang = 'sv'; usage.textContent = selected.UsageLabel; reply.append(usage); }
            log.lastElementChild.append(reply);
            feedback.classList.add(result.isCorrect ? 'answer-correct' : 'answer-wrong');
            if (result.isCorrect) feedback.textContent = `Det passar! Ditt svar betyder: ${result.meaning}.`;
            else {
                const intro = document.createElement('p'); intro.textContent = 'Inte riktigt i den här situationen. Ett passande svar är:';
                const correct = document.createElement('strong'); correct.lang = 'ti-Latn'; correct.textContent = result.correctText;
                const meaning = document.createElement('p'); meaning.textContent = result.meaning;
                feedback.replaceChildren(intro, correct, meaning);
            }
            if (result.usageLabel) { const usage = document.createElement('small'); usage.className = 'phrase-choice-label'; usage.textContent = result.usageLabel; feedback.append(usage); }
            if (result.explanation) { const why = document.createElement('p'); why.className = 'conversation-explanation'; why.textContent = result.explanation; feedback.append(why); }
            check.hidden = true; next.hidden = false;
            next.textContent = result.isComplete ? 'Visa resultat' : stepLabel === 'Situation' ? 'Nästa situation' : 'Nästa replik';
        } catch {
            feedback.textContent = 'Kunde inte nå samtalet. Kontrollera anslutningen och försök igen.';
            check.textContent = 'Försök igen';
        } finally {
            busy = false; refresh(); if (answered) next.focus();
        }
    }
    check.addEventListener('click', sendAnswer);
    next.addEventListener('click', () => {
        if (busy || !answered) return;
        if (index + 1 === board.Turns.length) { next.disabled = true; form.requestSubmit(); }
        else { index++; renderTurn(true); }
    });
    document.addEventListener('keydown', event => {
        if (busy || answered || pending || event.altKey || event.ctrlKey || event.metaKey) return;
        if (/^[1-4]$/.test(event.key)) {
            const option = buttons[Number(event.key) - 1];
            if (option) { event.preventDefault(); selected = option.choice; refresh(); option.button.focus(); }
        }
    });
    renderTurn(false); root.hidden = false; document.getElementById('conversation-fallback').hidden = true;
})();
