var AdminPagesJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askCheck = null, askBack = null;
    var dirty = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function qp(name) {
        return new URLSearchParams(location.search).get(name) || '';
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('hl_tok') });
        $ApiRequest('AdminPages/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'hl_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function signout() {
        api('SignOut');
        setTimeout(expired, 400);
    }

    function toast(msg) {
        var t = el('pg-toast');
        t.textContent = msg;
        t.classList.add('pg-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pg-open');
        }, 3600);
    }

    function openMenu() {
        el('pg-rail').classList.add('pg-open');
        el('pg-scrim').classList.add('pg-open');
    }

    function closeMenu() {
        el('pg-rail').classList.remove('pg-open');
        el('pg-scrim').classList.remove('pg-open');
    }

    function openModal(title) {
        var m = el('pg-modal');
        el('pg-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('pg-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('pg-modal');
        if (m) {
            m.classList.remove('pg-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('pg-modal');
        if (!m || !m.classList.contains('pg-open')) {
            return;
        }
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, shut);
            return;
        }
        shut();
    }

    function markDirty() {
        dirty = true;
    }

    function clean() {
        dirty = false;
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('pg-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('pg-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        var m = askCheck ? askCheck(x.i.value.trim()) : '';
        if (m) {
            x.e.textContent = m;
            x.i.focus();
            return;
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'pg-ask';
        askBox.innerHTML = '<div class="pg-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="pg-err" role="alert"></em><div class="pg-askacts"><button type="button" class="pg-btn pg-btn-ghost"></button><button type="button" class="pg-btn"></button></div></div>';
        el('pg-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('pg-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts(), input = typeof o.value === 'string';
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'pg-btn' + (o.danger ? ' pg-askdel' : '');
        askBox.firstChild.classList.toggle('pg-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('pg-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function collect(root, extra) {
        var list = extra || [];
        document.querySelectorAll('#' + root + ' [data-k]').forEach(function (n) {
            if (n.type === 'checkbox') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.checked ? '1' : '0' });
            } else if (n.type === 'password') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.value });
            } else {
                list.push({ key: n.getAttribute('data-k'), vlu: (n.value || '').trim() });
            }
        });
        return list;
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    var page = 'home';
    var dirty = false;
    var drag = null;
    var startOrder = '';
    var mediaFor = null;
    var richAt = null;
    var range = null;
    var rtTimer = null;
    var pkAllow = '';
    var pkDone = null;
    var pkTimer = null;

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function loaded() {
        show(qs('p') || 'home');
        window.addEventListener('beforeunload', function (e) {
            if (dirty) {
                e.preventDefault();
                e.returnValue = '';
            }
        });
    }

    function show(p) {
        api('Show', [{ key: 'page', vlu: p }]);
    }

    function tab(p) {
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes to this page will be lost.', ok: 'Leave', no: 'Stay', danger: true }, function () {
                dirty = false;
                show(p);
            });
            return;
        }
        show(p);
    }

    function mark() {
        var d = el('pg-dirty');
        if (d) {
            d.textContent = dirty ? 'Unsaved changes' : '';
        }
    }

    function touch() {
        dirty = true;
        mark();
    }

    function shown(p, href) {
        page = p;
        dirty = false;
        mark();
        var v = el('pg-viewlink');
        if (v) {
            v.setAttribute('data-href', href);
        }
        try {
            history.replaceState(null, '', 'AdminPages?p=' + p);
        } catch (e) {
        }
        var c = el('pg-canvas');
        if (!c || c.getAttribute('data-bound')) {
            return;
        }
        c.setAttribute('data-bound', '1');
        c.addEventListener('input', touch);
        c.addEventListener('change', touch);
        c.addEventListener('paste', function (e) {
            var t = e.target.closest ? e.target.closest('[contenteditable]') : null;
            if (t) {
                e.preventDefault();
                var txt = (e.clipboardData || window.clipboardData).getData('text/plain');
                document.execCommand('insertText', false, t.getAttribute('data-ml') === '1' || t.classList.contains('pg-er') ? txt : txt.replace(/\s*\n\s*/g, ' '));
            }
        });
        c.addEventListener('keydown', function (e) {
            var t = e.target;
            if (e.key === 'Enter' && t.getAttribute && t.getAttribute('contenteditable') === 'plaintext-only' && t.getAttribute('data-ml') !== '1') {
                e.preventDefault();
            }
        });
        c.addEventListener('focusin', function (e) {
            if (e.target.classList && e.target.classList.contains('pg-er')) {
                richAt = e.target;
                clearTimeout(rtTimer);
                tools(true);
            }
        });
        window.addEventListener('scroll', function () {
            if (richAt && el('pg-rt').classList.contains('pg-open')) {
                tools(true);
            }
        }, { passive: true });
        c.addEventListener('focusout', function (e) {
            if (e.target.classList && e.target.classList.contains('pg-er')) {
                rtTimer = setTimeout(function () {
                    tools(false);
                }, 180);
            }
        });
    }

    function tools(on) {
        var t = el('pg-rt');
        if (!t) {
            return;
        }
        if (!on || !richAt) {
            t.classList.remove('pg-open');
            return;
        }
        var r = richAt.getBoundingClientRect();
        t.style.left = Math.max(8, Math.min(r.left, window.innerWidth - 260)) + 'px';
        t.style.top = Math.max(64, r.top - 42) + 'px';
        t.classList.add('pg-open');
    }

    function fmt(cmd) {
        if (!richAt) {
            return;
        }
        richAt.focus();
        if (cmd === 'link') {
            var sel = window.getSelection();
            range = sel.rangeCount ? sel.getRangeAt(0).cloneRange() : null;
            if (!range || range.collapsed) {
                toast('Select some words first, then add the link.');
                return;
            }
            var target = richAt;
            ask({ title: 'Add a link', text: 'A page name like Shop or Contact, or a full address starting with https://', value: 'https://', ok: 'Add link', check: function (v) {
                return /^(https?:\/\/\S+|mailto:\S+|tel:[0-9+() .-]+|#[A-Za-z0-9_-]+|[A-Za-z][A-Za-z0-9_-]*(\?[A-Za-z0-9=&%._+-]*)?(#[A-Za-z0-9_-]+)?)$/.test(v) ? '' : 'Use a page name or a full https:// address.';
            } }, function (v) {
                target.focus();
                var s2 = window.getSelection();
                s2.removeAllRanges();
                s2.addRange(range);
                document.execCommand('createLink', false, v);
                touch();
            });
            return;
        }
        if (cmd === 'clear') {
            document.execCommand('removeFormat');
            document.execCommand('unlink');
        } else {
            document.execCommand(cmd);
        }
        touch();
    }

    function blocks() {
        return el('pg-canvas').querySelectorAll(':scope > .pg-blk');
    }

    function order() {
        return [].map.call(blocks(), function (b) {
            return b.getAttribute('data-id');
        }).join(',');
    }

    function grab(e, btn) {
        e.preventDefault();
        drag = btn.closest('.pg-blk');
        startOrder = order();
        drag.classList.add('pg-dragging');
        document.addEventListener('pointermove', dragMove);
        document.addEventListener('pointerup', dragEnd);
        document.addEventListener('pointercancel', dragEnd);
    }

    function dragMove(ev) {
        if (!drag) {
            return;
        }
        ev.preventDefault();
        var t = document.elementFromPoint(ev.clientX, ev.clientY);
        var over = t && t.closest ? t.closest('#pg-canvas > .pg-blk') : null;
        if (over && over !== drag) {
            var r = over.getBoundingClientRect();
            over.parentNode.insertBefore(drag, ev.clientY > r.top + r.height / 2 ? over.nextSibling : over);
        }
        if (ev.clientY < 120) {
            window.scrollBy(0, -16);
        } else if (ev.clientY > window.innerHeight - 60) {
            window.scrollBy(0, 16);
        }
    }

    function dragEnd() {
        document.removeEventListener('pointermove', dragMove);
        document.removeEventListener('pointerup', dragEnd);
        document.removeEventListener('pointercancel', dragEnd);
        if (!drag) {
            return;
        }
        drag.classList.remove('pg-dragging');
        drag = null;
        if (order() !== startOrder) {
            touch();
        }
    }

    function move(btn, dir) {
        var b = btn.closest('.pg-blk');
        var sib = dir < 0 ? b.previousElementSibling : b.nextElementSibling;
        if (!sib || !sib.classList.contains('pg-blk')) {
            return;
        }
        if (dir < 0) {
            b.parentNode.insertBefore(b, sib);
        } else {
            b.parentNode.insertBefore(sib, b);
        }
        b.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        touch();
    }

    function hide(btn) {
        var b = btn.closest('.pg-blk');
        var on = b.getAttribute('data-hidden') !== '1';
        b.setAttribute('data-hidden', on ? '1' : '0');
        b.classList.toggle('pg-hid', on);
        btn.querySelector('span').textContent = on ? 'Show' : 'Hide';
        btn.setAttribute('aria-pressed', on ? 'true' : 'false');
        touch();
    }

    function drop(btn) {
        var b = btn.closest('.pg-blk');
        ask({ title: 'Remove this block?', text: 'It disappears from the page when you save. Use Hide to keep it for later.', ok: 'Remove', no: 'Keep it', danger: true }, function () {
            b.parentNode.removeChild(b);
            touch();
        });
    }

    function media(btn) {
        mediaFor = btn.closest('.pg-em');
        var allow = mediaFor.getAttribute('data-allow') || '';
        pickMedia(allow, function (it) {
            mediaFor.setAttribute('data-src', it.src);
            paintMedia(mediaFor, it);
            touch();
        });
    }

    function noMedia(btn) {
        var m = btn.closest('.pg-em');
        m.setAttribute('data-src', '');
        paintMedia(m, null);
        touch();
    }

    function paintMedia(m, it) {
        var b = m.querySelector('.pg-emb');
        if (!it) {
            b.innerHTML = '<span>' + esc(m.getAttribute('data-ph') || 'Add a picture') + '</span>';
        } else if (it.kind === 'video') {
            b.innerHTML = '<video src="' + esc(it.src) + '#t=0.1" muted preload="metadata" playsinline></video><span class="pg-emv">Video</span><span class="pg-emc">Change</span>';
        } else {
            b.innerHTML = '<img src="' + esc(it.thumb) + '" alt=""><span class="pg-emc">Change</span>';
        }
        var x = m.querySelector('.pg-emx');
        if (x) {
            x.hidden = !it;
        }
    }

    function pickMedia(allow, done) {
        pkAllow = allow;
        pkDone = done;
        el('pg-pkt').textContent = allow === 'photo' ? 'Choose a photo' : 'Choose a photo or video';
        el('pg-pkq').value = '';
        el('pg-pkmsg').textContent = '';
        var k = el('pg-pkkind');
        k.value = allow;
        k.disabled = !!allow;
        el('pg-pkfile').setAttribute('accept', allow === 'photo' ? 'image/jpeg,image/png,image/webp,image/gif' : 'image/jpeg,image/png,image/webp,image/gif,video/mp4');
        var d = el('pg-pk');
        d.classList.add('pg-open');
        d.setAttribute('aria-hidden', 'false');
        pkList();
        setTimeout(function () {
            el('pg-pkq').focus();
        }, 30);
    }

    function pkList() {
        api('MediaPick', [{ key: 'pallow', vlu: pkAllow }, { key: 'pkind', vlu: val('pg-pkkind') }, { key: 'pterm', vlu: val('pg-pkq') }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(pkList, 300);
    }

    function pkPick(b) {
        var f = pkDone;
        var it = { src: b.getAttribute('data-src'), thumb: b.getAttribute('data-thumb'), kind: b.getAttribute('data-kind') };
        pkClose();
        if (f) {
            f(it);
        }
    }

    function pkOpen() {
        var d = el('pg-pk');
        return d && d.classList.contains('pg-open');
    }

    function pkClose() {
        var d = el('pg-pk');
        if (d) {
            d.classList.remove('pg-open');
            d.setAttribute('aria-hidden', 'true');
        }
        el('pg-pkfile').value = '';
        pkDone = null;
    }

    function pkUpload() {
        el('pg-pkmsg').textContent = 'Uploading. Large videos can take a minute.';
        api('MediaUp', [{ key: 'pallow', vlu: pkAllow }]);
    }

    function pkUploaded(ok, msg) {
        el('pg-pkfile').value = '';
        el('pg-pkmsg').textContent = msg;
        if (ok) {
            el('pg-pkq').value = '';
            el('pg-pkkind').value = pkAllow;
        }
    }

    function addBlock(type) {
        api('AddBlock', [{ key: 'btype', vlu: type }]);
    }

    function added() {
        var h = el('pg-holder');
        var b = h.firstElementChild;
        if (!b) {
            return;
        }
        el('pg-canvas').insertBefore(b, el('pg-addp'));
        b.scrollIntoView({ block: 'center', behavior: 'smooth' });
        var f = b.querySelector('[contenteditable]');
        if (f) {
            setTimeout(function () {
                f.focus();
            }, 300);
        }
        touch();
    }

    function addItem(btn) {
        var box = btn.parentNode.querySelector('.pg-eitems');
        var tpl = btn.parentNode.querySelector('template');
        if (box.children.length >= 12) {
            toast('A block can have up to 12 items.');
            return;
        }
        box.insertAdjacentHTML('beforeend', tpl.innerHTML);
        var f = box.lastElementChild.querySelector('[contenteditable]');
        if (f) {
            f.focus();
        }
        touch();
    }

    function itemMove(btn, dir) {
        var it = btn.closest('.pg-eit');
        var sib = dir < 0 ? it.previousElementSibling : it.nextElementSibling;
        if (!sib) {
            return;
        }
        if (dir < 0) {
            it.parentNode.insertBefore(it, sib);
        } else {
            it.parentNode.insertBefore(sib, it);
        }
        touch();
    }

    function itemDrop(btn) {
        var it = btn.closest('.pg-eit');
        it.parentNode.removeChild(it);
        touch();
    }

    function cleanNode(n) {
        var out = '';
        [].forEach.call(n.childNodes, function (c) {
            if (c.nodeType === 3) {
                out += esc(c.nodeValue);
                return;
            }
            if (c.nodeType !== 1) {
                return;
            }
            var t = c.tagName.toLowerCase();
            if (/^(script|style|template|iframe|object|img|video|svg)$/.test(t)) {
                return;
            }
            var inner = cleanNode(c);
            if (t === 'br') {
                out += '<br>';
            } else if (t === 'b' || t === 'strong') {
                out += inner ? '<b>' + inner + '</b>' : '';
            } else if (t === 'i' || t === 'em') {
                out += inner ? '<i>' + inner + '</i>' : '';
            } else if (t === 'a') {
                var h = c.getAttribute('href') || '';
                out += h ? '<a href="' + esc(h) + '">' + inner + '</a>' : inner;
            } else if (t === 'ul' || t === 'ol' || t === 'li') {
                out += '<' + t + '>' + inner + '</' + t + '>';
            } else if (t === 'p' || t === 'div' || /^h[1-6]$/.test(t)) {
                out += '<p>' + inner + '</p>';
            } else {
                out += inner;
            }
        });
        return out;
    }

    function textOf(f) {
        if (f.classList.contains('pg-er')) {
            return cleanNode(f);
        }
        return (f.innerText || '').replace(/ /g, ' ').replace(/\n{3,}/g, '\n\n').trim();
    }

    function collect() {
        return [].map.call(blocks(), function (b) {
            var o = { id: b.getAttribute('data-id'), type: b.getAttribute('data-type'), source: b.getAttribute('data-src') || '', hidden: b.getAttribute('data-hidden') === '1', kicker: '', title: '', text: '', media: '', label: '', link: '', label2: '', link2: '', flip: false, items: [] };
            [].forEach.call(b.querySelectorAll('[data-f]'), function (f) {
                var k = f.getAttribute('data-f');
                if (k === 'media') {
                    o.media = f.getAttribute('data-src') || '';
                } else if (k === 'flip') {
                    o.flip = f.checked;
                } else if (k === 'items') {
                    o.items = [].map.call(f.querySelectorAll(':scope > .pg-eit'), function (it) {
                        var r = { icon: '', media: '', title: '', text: '', link: '', label: '' };
                        [].forEach.call(it.querySelectorAll('[data-i]'), function (x) {
                            var n = x.getAttribute('data-i');
                            r[n] = n === 'media' ? (x.getAttribute('data-src') || '') : x.tagName === 'INPUT' || x.tagName === 'SELECT' ? x.value.trim() : textOf(x);
                        });
                        return r;
                    });
                } else if (f.tagName === 'INPUT' || f.tagName === 'SELECT') {
                    o[k] = f.value.trim();
                } else {
                    o[k] = textOf(f);
                }
            });
            return o;
        });
    }

    function save() {
        tools(false);
        el('pg-savebtn').disabled = true;
        api('Save', [{ key: 'page', vlu: page }, { key: 'blocks', vlu: JSON.stringify(collect()) }]);
    }

    function saved(msg) {
        var b = el('pg-savebtn');
        if (b) {
            b.disabled = false;
        }
        dirty = false;
        mark();
        toast(msg);
    }

    function fail(msg) {
        el('pg-savebtn').disabled = false;
        toast(msg);
    }

    function versions() {
        api('Versions', [{ key: 'page', vlu: page }]);
    }

    function opened(title) {
        openModal(title);
    }

    function restore(i, when) {
        ask({ title: 'Restore the version from ' + when + '?', text: 'The page goes back to that version right away. The current version is kept in the list, so you can switch back.', ok: 'Restore', no: 'Cancel' }, function () {
            shut();
            api('Restore', [{ key: 'page', vlu: page }, { key: 'vi', vlu: String(i) }]);
        });
    }

    function view() {
        if (dirty) {
            toast('Save first to see your changes on the website.');
        }
        window.open(el('pg-viewlink').getAttribute('data-href'), '_blank', 'noopener');
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('pg-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('pg-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
    window.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && pkOpen() && !(askBox && askBox.classList.contains('pg-open'))) {
            e.stopPropagation();
            pkClose();
        }
    }, true);
    var d = el('pg-pk');
    if (d) {
        d.addEventListener('mousedown', function (e) {
            if (e.target === d) {
                pkClose();
            }
        });
    }
        api('Load', [{ key: 'view', vlu: qp('f') }, { key: 'show', vlu: qp('show') }, { key: 'from', vlu: qp('from') }]);
    }

    return {
        reveal: reveal,
        expired: expired,
        signout: signout,
        toast: toast,
        openMenu: openMenu,
        closeMenu: closeMenu,
        openModal: openModal,
        closeModal: closeModal,
        shut: shut,
        clean: clean,
        loaded: loaded,
        tab: tab,
        shown: shown,
        fmt: fmt,
        grab: grab,
        move: move,
        hide: hide,
        drop: drop,
        media: media,
        noMedia: noMedia,
        pkList: pkList,
        pkSearch: pkSearch,
        pkPick: pkPick,
        pkClose: pkClose,
        pkUpload: pkUpload,
        pkUploaded: pkUploaded,
        addBlock: addBlock,
        added: added,
        addItem: addItem,
        itemMove: itemMove,
        itemDrop: itemDrop,
        save: save,
        saved: saved,
        fail: fail,
        versions: versions,
        opened: opened,
        restore: restore,
        view: view
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminPagesJs.reveal);
} else {
    AdminPagesJs.reveal();
}
