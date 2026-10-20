var ServiceAreaJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('ServiceArea/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('sa-toast');
        t.textContent = msg;
        t.classList.add('sa-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sa-open');
        }, 3400);
    }

    function openNav() {
        el('sa-nav').classList.add('sa-open');
        el('sa-scrim').classList.add('sa-open');
    }

    function closeNav() {
        el('sa-nav').classList.remove('sa-open');
        el('sa-scrim').classList.remove('sa-open');
    }

    function zip() {
        call('CheckZip', [{ key: 'zipcode', vlu: val('sa-zipin') }]);
        return false;
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
        zip: zip
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ServiceAreaJs.reveal);
} else {
    ServiceAreaJs.reveal();
}
