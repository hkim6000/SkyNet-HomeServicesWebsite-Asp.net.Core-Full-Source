var QuoteJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Quote/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('qt-toast');
        t.textContent = msg;
        t.classList.add('qt-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('qt-open');
        }, 3400);
    }

    function openNav() {
        el('qt-nav').classList.add('qt-open');
        el('qt-scrim').classList.add('qt-open');
    }

    function closeNav() {
        el('qt-nav').classList.remove('qt-open');
        el('qt-scrim').classList.remove('qt-open');
    }

    var ptimer = null;

    function checked(name) {
        var out = [];
        document.querySelectorAll('input[name="' + name + '"]:checked').forEach(function (n) {
            out.push(n.value);
        });
        return out.join(',');
    }

    function photos() {
        var out = [];
        document.querySelectorAll('#qt-photos .qt-pt').forEach(function (n) {
            out.push(n.getAttribute('data-p'));
        });
        return out.join(',');
    }

    function pick() {
        clearTimeout(ptimer);
        ptimer = setTimeout(function () {
            call('Pick', [{ key: 'services', vlu: checked('qt-svc') }, { key: 'zipcode', vlu: val('qt-zip') }]);
        }, 200);
    }

    function upload() {
        el('qt-e-photos').textContent = 'Uploading...';
        call('Upload', [{ key: 'photos', vlu: photos() }]);
    }

    function uploaded(n) {
        var f = el('qt-file');
        if (f) {
            f.value = '';
        }
        el('qt-upl').hidden = n >= 6;
    }

    function removePhoto(p) {
        call('RemovePhoto', [{ key: 'photos', vlu: photos() }, { key: 'drop', vlu: p }]);
    }

    function count() {
        var n = el('qt-desc').value.length;
        el('qt-count').textContent = n.toLocaleString('en-US') + ' of 2,000 characters';
    }

    function submit() {
        call('Submit', [
            { key: 'services', vlu: checked('qt-svc') },
            { key: 'desc', vlu: val('qt-desc') },
            { key: 'photos', vlu: photos() },
            { key: 'days', vlu: checked('qt-day') },
            { key: 'times', vlu: checked('qt-time') },
            { key: 'name', vlu: val('qt-name') },
            { key: 'phone', vlu: val('qt-phone') },
            { key: 'email', vlu: val('qt-email') },
            { key: 'address', vlu: val('qt-address') },
            { key: 'zipcode', vlu: val('qt-zip') }
        ]);
        return false;
    }

    function failed() {
        var errs = Array.prototype.filter.call(document.querySelectorAll('#qt-qform .qt-err'), function (n) {
            return n.textContent.trim() !== '' && n.id !== 'qt-e-photos';
        });
        if (errs.length) {
            errs[0].closest('.qt-step').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
        toast('Please check the marked fields');
    }

    function done() {
        var b = el('qt-donebox');
        if (b) {
            b.scrollIntoView({ behavior: 'smooth', block: 'start' });
            b.focus({ preventScroll: true });
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
        pick: pick,
        upload: upload,
        uploaded: uploaded,
        removePhoto: removePhoto,
        count: count,
        submit: submit,
        failed: failed,
        done: done
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', QuoteJs.reveal);
} else {
    QuoteJs.reveal();
}
