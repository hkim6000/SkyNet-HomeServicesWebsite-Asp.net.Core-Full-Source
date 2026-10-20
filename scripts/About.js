var AboutJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('About/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('ab-toast');
        t.textContent = msg;
        t.classList.add('ab-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ab-open');
        }, 3400);
    }

    function openNav() {
        el('ab-nav').classList.add('ab-open');
        el('ab-scrim').classList.add('ab-open');
    }

    function closeNav() {
        el('ab-nav').classList.remove('ab-open');
        el('ab-scrim').classList.remove('ab-open');
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AboutJs.reveal);
} else {
    AboutJs.reveal();
}
