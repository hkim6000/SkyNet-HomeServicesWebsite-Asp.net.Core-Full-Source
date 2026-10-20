var QuoteViewJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('QuoteView/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('qv-toast');
        t.textContent = msg;
        t.classList.add('qv-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('qv-open');
        }, 3400);
    }

    function openNav() {
        el('qv-nav').classList.add('qv-open');
        el('qv-scrim').classList.add('qv-open');
    }

    function closeNav() {
        el('qv-nav').classList.remove('qv-open');
        el('qv-scrim').classList.remove('qv-open');
    }

    function qkey() {
        var m = location.search.match(/[?&]k=([a-f0-9]{32})/);
        return m ? m[1] : '';
    }

    function accept() {
        call('Accept', [
            { key: 'qk', vlu: qkey() },
            { key: 'sig', vlu: val('qv-sig') },
            { key: 'last4', vlu: val('qv-last4') },
            { key: 'agree', vlu: el('qv-agree').checked ? '1' : '0' }
        ]);
        return false;
    }

    function declineOpen() {
        el('qv-declbox').hidden = false;
        el('qv-declbtn').hidden = true;
        el('qv-why').focus();
    }

    function decline() {
        call('Decline', [
            { key: 'qk', vlu: qkey() },
            { key: 'last4', vlu: val('qv-last4') },
            { key: 'why', vlu: val('qv-why') }
        ]);
    }

    function answered(msg) {
        toast(msg);
        var a = el('qv-act');
        if (a) {
            a.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
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
        accept: accept,
        declineOpen: declineOpen,
        decline: decline,
        answered: answered
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', QuoteViewJs.reveal);
} else {
    QuoteViewJs.reveal();
}
