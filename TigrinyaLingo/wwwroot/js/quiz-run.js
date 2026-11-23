// Ett steg i taget. Servern rättar både direktåterkopplingen och den slutliga inskickningen.
(() => {
    const form = document.querySelector('[data-quiz-run]');
    if (!form || !window.fetch || !form.requestSubmit) return;
    const stages = [...form.querySelectorAll('[data-question]')];
    if (!stages.length) return;
    const discoveries = [...form.querySelectorAll('[data-learn-step]')];
    const finish = form.parentElement.querySelector('[data-speaking-finish]');
    const button = form.querySelector('[data-quiz-action]');
    const submit = form.querySelector('[data-quiz-submit]');
    const progress = form.querySelector('[data-quiz-progress]');
    const feedback = form.querySelector('[data-feedback]');
    const title = form.querySelector('[data-feedback-title]');
    const detail = form.querySelector('[data-feedback-detail]');
    let current = 0, checked = false, busy = false, correctCount = 0, readyToSubmit = false, learning = false, finishing = false;
    form.noValidate = true;
    form.classList.add('quiz-enhanced');
    form.querySelector('[data-quiz-hud]').hidden = false;
    form.querySelector('[data-quiz-controls]').hidden = false;
    submit.hidden = true;

    function pauseAudio() {
        for (const audio of form.querySelectorAll('audio')) audio.pause();
    }
    function focusTitle(selector, parent) {
        const heading = parent.querySelector(selector);
        if (heading) { heading.tabIndex = -1; heading.focus(); }
    }
    function showStage(study = true) {
        pauseAudio();
        learning = study && discoveries.length === stages.length;
        stages.forEach((stage, i) => { stage.hidden = i !== current || learning; });
        discoveries.forEach((step, i) => { step.hidden = i !== current || !learning; });
        checked = false;
        feedback.className = 'quiz-feedback';
        title.textContent = learning ? 'Lär dig uttrycket' : 'Välj ett alternativ';
        detail.textContent = learning ? 'Ta den tid du behöver. Sedan får du prova själv.' : 'Du kan också använda tangenterna 1–4.';
        button.textContent = learning ? 'Öva på uttrycket' : 'Kontrollera';
        button.disabled = !learning && !stages[current].querySelector('input[type="radio"]:checked');
        form.querySelector('[data-step-label]').textContent = learning
            ? `Uttryck ${current + 1} av ${stages.length} · Lär dig`
            : `Fråga ${current + 1} av ${stages.length}`;
    }
    function saveLesson() {
        if (busy) return;
        busy = true;
        readyToSubmit = true;
        button.disabled = true;
        button.textContent = 'Sparar…';
        const finishButton = finish?.querySelector('[data-finish-lesson]');
        if (finishButton) { finishButton.disabled = true; finishButton.textContent = 'Sparar…'; }
        form.requestSubmit(submit);
    }
    finish?.querySelector('[data-finish-lesson]')?.addEventListener('click', saveLesson);
    showStage();
    form.addEventListener('change', () => {
        if (!busy && !checked && !learning) button.disabled = !stages[current].querySelector('input[type="radio"]:checked');
    });
    async function advance() {
        if (busy || finishing) return;
        if (learning) {
            showStage(false);
            focusTitle('[data-stage-title]', stages[current]);
            return;
        }
        if (checked) {
            if (current === stages.length - 1) {
                if (finish) {
                    finishing = true;
                    pauseAudio();
                    form.hidden = true;
                    finish.hidden = false;
                    focusTitle('h2', finish);
                } else saveLesson();
            } else {
                current++;
                showStage();
                focusTitle(learning ? '[data-learn-title]' : '[data-stage-title]', learning ? discoveries[current] : stages[current]);
            }
            return;
        }
        const stage = stages[current];
        const selected = stage.querySelector('input[type="radio"]:checked');
        if (!selected) {
            title.textContent = 'Välj ett svar först';
            return;
        }
        busy = true;
        button.disabled = true;
        button.textContent = 'Kontrollerar…';
        try {
            const body = new URLSearchParams({
                SessionToken: form.querySelector('[name="SessionToken"]')?.value ?? '',
                QuestionId: stage.dataset.questionId,
                ChoiceId: selected.value,
                Kind: stage.dataset.kind,
                __RequestVerificationToken: form.querySelector('[name="__RequestVerificationToken"]').value
            });
            const response = await fetch(form.dataset.checkUrl, {
                method: 'POST', credentials: 'same-origin',
                headers: {'Content-Type': 'application/x-www-form-urlencoded'}, body
            });
            if (!response.ok || response.redirected) throw new Error('Kunde inte rätta svaret');
            const result = await response.json();
            if (typeof result.isCorrect !== 'boolean' || typeof result.correctChoiceId !== 'number') throw new Error('Ogiltigt svar');
            // Lås det valda svaret, men behåll en dold kopia till den slutliga rättningen.
            const saved = document.createElement('input');
            saved.type = 'hidden';
            saved.name = selected.name;
            saved.value = selected.value;
            stage.append(saved);
            for (const radio of stage.querySelectorAll('input[type="radio"]')) {
                radio.disabled = true;
                if (Number(radio.value) === result.correctChoiceId) radio.closest('.quiz-option').classList.add('answer-correct');
            }
            if (!result.isCorrect) selected.closest('.quiz-option').classList.add('answer-wrong');
            else correctCount++;
            feedback.classList.add(result.isCorrect ? 'feedback-correct' : 'feedback-wrong');
            title.textContent = result.isCorrect ? 'Snyggt! Rätt svar.' : 'Nästan! Vi tar det igen i repetitionen.';
            detail.textContent = result.isCorrect ? 'Du bygger ditt ordförråd, ett steg i taget.' : `Rätt svar: ${result.correctAnswer}`;
            if (result.explanation) detail.textContent += ' ' + result.explanation;
            progress.value = current + 1;
            form.querySelector('[data-correct-label]').textContent = `${correctCount} rätt`;
            checked = true;
            button.textContent = current === stages.length - 1 ? (finish ? 'Öva högt' : 'Visa resultat') : 'Fortsätt';
        } catch {
            title.textContent = 'Svaret kunde inte kontrolleras';
            detail.textContent = 'Försök igen. Om sessionen har gått ut behöver du ladda om sidan och logga in.';
            button.textContent = 'Försök igen';
        } finally {
            busy = false;
            button.disabled = false;
        }
    }
    button.addEventListener('click', advance);
    form.addEventListener('submit', event => {
        if (!readyToSubmit) {
            event.preventDefault();
            advance();
        }
    });
    form.addEventListener('keydown', event => {
        if (!busy && !checked && !learning && !finishing && /^[1-4]$/.test(event.key) && !event.ctrlKey && !event.altKey && !event.metaKey) {
            event.preventDefault();
            const radio = stages[current].querySelectorAll('input[type="radio"]')[Number(event.key) - 1];
            radio?.click();
            radio?.focus();
        }
    });
})();