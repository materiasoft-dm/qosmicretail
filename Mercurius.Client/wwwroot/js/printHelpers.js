// Prints a single image (e.g. a product's QR code) on its own page, captioned, without the rest
// of the app around it. The caption is set via textContent so a product name can never inject
// markup into the print window.
window.mercuriusPrint = {
    printImage: function (url, caption) {
        var w = window.open('', '_blank', 'width=480,height=560');
        if (!w) {
            return false; // popup blocked
        }
        var doc = w.document;
        doc.open();
        doc.write('<!DOCTYPE html><html><head><meta charset="utf-8"><title></title>' +
            '<style>body{margin:0;display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;font-family:sans-serif}' +
            'img{width:260px;height:260px}p{margin:8px 0 0;font-size:14px;text-align:center}</style></head>' +
            '<body><img id="print-image" alt=""><p id="print-caption"></p></body></html>');
        doc.close();
        doc.title = caption || '';
        doc.getElementById('print-caption').textContent = caption || '';
        var img = doc.getElementById('print-image');
        img.onload = function () {
            w.focus();
            w.onafterprint = function () { w.close(); };
            w.print();
        };
        img.src = new URL(url, window.location.origin).href;
        return true;
    },

    // Prints a titled report: summary figures, then a table that repeats its header on each page.
    // report = { title, subtitle, summary: [{ label, value }], columns: [text], rightAligned: [index], rows: [[text]] }.
    // Everything goes in via textContent, for the same reason as the caption above.
    printReport: function (report) {
        var w = window.open('', '_blank', 'width=1000,height=700');
        if (!w) {
            return false; // popup blocked
        }
        var doc = w.document;
        doc.open();
        doc.write('<!DOCTYPE html><html><head><meta charset="utf-8"><title></title>' +
            '<style>@page{size:landscape;margin:12mm}' +
            'body{margin:16px;font-family:sans-serif;font-size:11px;color:#000}' +
            'h1{margin:0;font-size:18px}.subtitle{margin:2px 0 12px;color:#444}' +
            '.summary{display:flex;flex-wrap:wrap;gap:8px 28px;margin-bottom:14px}' +
            '.summary div div:first-child{color:#444}.summary div div:last-child{font-size:14px;font-weight:600}' +
            'table{width:100%;border-collapse:collapse}thead{display:table-header-group}tr{page-break-inside:avoid}' +
            'th,td{padding:4px 6px;border-bottom:1px solid #bbb;text-align:left;vertical-align:top}' +
            'th{border-bottom:2px solid #000}.right{text-align:right;white-space:nowrap}</style></head><body></body></html>');
        doc.close();
        doc.title = report.title;

        function add(parent, tag, text, className) {
            var el = doc.createElement(tag);
            if (text !== undefined && text !== null) el.textContent = text;
            if (className) el.className = className;
            parent.appendChild(el);
            return el;
        }

        add(doc.body, 'h1', report.title);
        add(doc.body, 'p', report.subtitle, 'subtitle');
        var summary = add(doc.body, 'div', null, 'summary');
        report.summary.forEach(function (item) {
            var box = add(summary, 'div');
            add(box, 'div', item.label);
            add(box, 'div', item.value);
        });

        if (report.rows.length > 0) {
            function cellClass(i) { return report.rightAligned.indexOf(i) >= 0 ? 'right' : null; }
            var table = add(doc.body, 'table');
            var head = add(add(table, 'thead'), 'tr');
            report.columns.forEach(function (text, i) { add(head, 'th', text, cellClass(i)); });
            var body = add(table, 'tbody');
            report.rows.forEach(function (row) {
                var tr = add(body, 'tr');
                row.forEach(function (text, i) { add(tr, 'td', text, cellClass(i)); });
            });
        }

        // After a tick, so the print dialog doesn't hold up the call that opened this window.
        setTimeout(function () {
            w.focus();
            w.onafterprint = function () { w.close(); };
            w.print();
        }, 100);
        return true;
    }
};
