(() => {
    const form = document.querySelector('[data-placement-form]');
    if (!form) return;
    const questions = [...form.querySelectorAll('[data-placement-question]')];
    const back = form.querySelector('[data-placement-back]');
    const next = form.querySelector('[data-placement-next]');
    const submit = form.querySelector('[data-placement-submit]');
    const meter = document.querySelector('[data-placement-meter]');
    const progress = meter.querySelector('progress');
    const count = meter.querySelector('[data-placement-count]');
    let index = Math.max(0, questions.findIndex(q => !q.querySelector('input[type=radio]:checked')));
    function show(focus) {
        questions.forEach((q, i) => { q.hidden = i !== index; });
        const answered = !!questions[index].querySelector('input[type=radio]:checked');
        back.hidden = index === 0;
        next.hidden = index === questions.length - 1;
        next.disabled = !answered;
        submit.hidden = index !== questions.length - 1;
        submit.disabled = !answered;
        count.textContent = `Fråga ${index + 1} av ${questions.length}`;
        progress.value = index + 1;
        if (focus) {
            const heading = questions[index].querySelector('h2');
            heading.tabIndex = -1;
            heading.focus();
        }
    }
    form.addEventListener('change', () => show(false));
    back.addEventListener('click', () => { if (index > 0) { index--; show(true); } });
    next.addEventListener('click', () => {
        if (questions[index].querySelector('input[type=radio]:checked') && index < questions.length - 1) { index++; show(true); }
    });
    form.noValidate = true;
    form.addEventListener('submit', event => {
        const missing = questions.findIndex(q => !q.querySelector('input[type=radio]:checked'));
        if (missing >= 0) { event.preventDefault(); index = missing; show(true); }
    });
    meter.hidden = false;
    show(false);
})();
