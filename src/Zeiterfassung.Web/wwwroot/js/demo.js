// Kleine Browser-Helfer der Demo. Keine Netzwerkzugriffe.
window.demo = {
    // Lädt eine im Speicher erzeugte Datei herunter (Ersatz für den Server-Export).
    download: function (name, type, base64) {
        var bin = atob(base64);
        var bytes = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
        var url = URL.createObjectURL(new Blob([bytes], { type: type }));
        var a = document.createElement('a');
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    // Hält --banner-h auf der echten Höhe des Demo-Bands (es bricht auf schmalen Bildschirmen um).
    watchBanner: function () {
        var el = document.querySelector('.demo-banner');
        if (!el) return;
        var set = function () { document.documentElement.style.setProperty('--banner-h', el.offsetHeight + 'px'); };
        set();
        if (window.ResizeObserver) new ResizeObserver(set).observe(el);
    }
};
