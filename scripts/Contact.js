var ContactJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Contact/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('ct-toast');
        t.textContent = msg;
        t.classList.add('ct-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ct-open');
        }, 3400);
    }

    function openNav() {
        el('ct-nav').classList.add('ct-open');
        el('ct-scrim').classList.add('ct-open');
    }

    function closeNav() {
        el('ct-nav').classList.remove('ct-open');
        el('ct-scrim').classList.remove('ct-open');
    }

    function send() {
        call('Send', [
            { key: 'name', vlu: val('ct-name') },
            { key: 'email', vlu: val('ct-email') },
            { key: 'phone', vlu: val('ct-phone') },
            { key: 'topic', vlu: val('ct-topic') },
            { key: 'message', vlu: val('ct-msg') }
        ]);
        return false;
    }

    function sent() {
        el('ct-msg').value = '';
        el('ct-topic').value = '';
        toast('Message sent');
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
        send: send,
        sent: sent
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ContactJs.reveal);
} else {
    ContactJs.reveal();
}
