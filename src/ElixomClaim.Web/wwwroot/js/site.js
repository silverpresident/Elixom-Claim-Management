// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Render semantic UTC instants in each viewer's local timezone. Views must supply an ISO-8601
// instant in the datetime attribute and opt in with data-utc-date.
(function () {
    var formatter = new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short"
    });

    document.querySelectorAll("[data-utc-date]").forEach(function (element) {
        var instant = new Date(element.dateTime);
        if (!Number.isNaN(instant.getTime())) {
            element.textContent = formatter.format(instant);
        }
    });
}());
