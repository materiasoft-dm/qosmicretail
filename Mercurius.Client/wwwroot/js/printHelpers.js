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
    }
};
