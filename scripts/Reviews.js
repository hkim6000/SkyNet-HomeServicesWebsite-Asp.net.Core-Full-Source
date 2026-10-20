var ReviewsJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Reviews/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('rv-toast');
        t.textContent = msg;
        t.classList.add('rv-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rv-open');
        }, 3400);
    }

    function openNav() {
        el('rv-nav').classList.add('rv-open');
        el('rv-scrim').classList.add('rv-open');
    }

    function closeNav() {
        el('rv-nav').classList.remove('rv-open');
        el('rv-scrim').classList.remove('rv-open');
    }

    function only(t) {
        document.querySelectorAll('#rv-rchips .rv-rchip').forEach(function (b) {
            b.classList.toggle('rv-act', b.getAttribute('data-t') === t);
        });
        document.querySelectorAll('#rv-revs .rv-rw').forEach(function (r) {
            r.hidden = t !== '' && r.getAttribute('data-t') !== t;
        });
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
        closeNav: closeNav,
        only: only
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ReviewsJs.reveal);
} else {
    ReviewsJs.reveal();
}
