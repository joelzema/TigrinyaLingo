// Visa namn- och kursfält bara när användaren skapar en ny lektion.
// Formuläret fungerar även utan JavaScript; servern validerar alltid placeringen.
(() => {
    const form = document.querySelector('[data-create-phrase]');
    if (!form) return;
    const destination = form.querySelector('[data-lesson-destination]');
    const newLesson = form.querySelector('[data-new-lesson]');
    if (!destination || !newLesson) return;
    const refresh = () => { newLesson.hidden = destination.value !== '0'; };
    destination.addEventListener('change', refresh);
    refresh();
})();
