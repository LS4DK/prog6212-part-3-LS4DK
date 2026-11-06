// When a form is submitted, switch its buttons to a "please wait" state.
// This stops accidental double-clicks (for example entering the same event twice)
// and shows that something is happening during slower steps such as image uploads.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('form').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            // A "Are you sure?" prompt that was cancelled already stopped the submit
            if (event.defaultPrevented) {
                return;
            }

            form.querySelectorAll('button[type="submit"]').forEach(function (button) {
                button.dataset.originalHtml = button.innerHTML;
                button.disabled = true;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>Please wait...';
            });
        });
    });
});

// If the browser shows a page again from its history (Back button), put the buttons back to normal
window.addEventListener('pageshow', function (event) {
    if (!event.persisted) {
        return;
    }

    document.querySelectorAll('button[type="submit"][data-original-html]').forEach(function (button) {
        button.disabled = false;
        button.innerHTML = button.dataset.originalHtml;
    });
});