// ============================================================
//  SocialApp — client-side behaviour
//  Keep it small and dependency-free. No jQuery, no Bootstrap JS.
// ============================================================
(function () {
    "use strict";

    var root = document.documentElement;

    // ---------- 1. THEME TOGGLE ----------
    // The <head> script already applied the saved/OS theme before paint.
    // Here we just wire the button and keep the label in sync.
    var toggle = document.getElementById("themeToggle");
    var label = document.getElementById("themeToggleLabel");

    function syncLabel() {
        if (!label) return;
        // Label describes the theme you'll switch TO — clearer than naming the current one.
        var isDark = root.getAttribute("data-theme") === "dark";
        label.textContent = isDark ? "Light" : "Dark";
    }

    function applyTheme(theme) {
        root.setAttribute("data-theme", theme);
        try { localStorage.setItem("theme", theme); } catch (e) { /* ignore */ }
        syncLabel();
    }

    if (toggle) {
        syncLabel();
        toggle.addEventListener("click", function () {
            var next = root.getAttribute("data-theme") === "dark" ? "light" : "dark";
            applyTheme(next);
        });
    }

    // ---------- 2. ACTIVE NAV HIGHLIGHT ----------
    // Mark the nav link that matches the current URL, so users always
    // know where they are (something X's rail does poorly on deep pages).
    var path = window.location.pathname.toLowerCase();

    function markActive(selector) {
        var links = document.querySelectorAll(selector);
        var best = null, bestLen = -1;
        links.forEach(function (a) {
            var href = (a.getAttribute("href") || "").toLowerCase();
            if (href === "" || href === "#") return;

            // data-match: extra prefix jo isi link ko active mane. Zaroorat is liye
            // padi ke /Profile/Me redirect ho kar /u/<username> ban jata hai, aur
            // phir href kisi bhi tarah path se match nahi karta.
            var extra = (a.getAttribute("data-match") || "").toLowerCase();
            if (extra && path.indexOf(extra) === 0) {
                best = a; bestLen = 999;
                return;
            }

            // Longest matching href wins (so "/profile/me" beats "/").
            var isMatch = href === "/" ? path === "/" : path.indexOf(href) === 0;
            if (isMatch && href.length > bestLen) { best = a; bestLen = href.length; }
        });
        if (best) best.classList.add("is-active");
    }
    markActive(".nav-item");
    markActive(".mobile-item");

    // ---------- 3. LIVE CHARACTER COUNTERS ----------
    // Any <textarea data-maxlength="N"> paired with a
    // [data-count-for="id"] element gets a live remaining-count.
    document.querySelectorAll("[data-count-for]").forEach(function (counter) {
        var field = document.getElementById(counter.getAttribute("data-count-for"));
        if (!field) return;
        var max = parseInt(field.getAttribute("maxlength") || field.getAttribute("data-maxlength") || "0", 10);
        function update() {
            var left = max - field.value.length;
            counter.textContent = left;
            counter.classList.toggle("is-over", left < 0);
        }
        field.addEventListener("input", update);
        update();
    });

    // ---------- 4. IMAGE PREVIEW BEFORE UPLOAD ----------
    // <input type="file" data-preview-for="boxId"> + a box containing an <img>.
    // Upload karne se pehle user ko dikhna chahiye ke usne kya chuna hai;
    // Facebook aur X dono yahi karte hain aur ye ab expected behaviour hai.
    document.querySelectorAll("[data-preview-for]").forEach(function (input) {
        var box = document.getElementById(input.getAttribute("data-preview-for"));
        if (!box) return;
        var img = box.querySelector("img");
        if (!img) return;

        function clear() {
            // revokeObjectURL zaroori hai: har createObjectURL browser ki memory
            // mein file ko zinda rakhta hai jab tak usse chhora na jaye.
            if (img.src.indexOf("blob:") === 0) URL.revokeObjectURL(img.src);
            img.removeAttribute("src");
            box.hidden = true;
        }

        input.addEventListener("change", function () {
            clear();
            var file = input.files && input.files[0];
            if (!file) return;
            img.src = URL.createObjectURL(file);
            box.hidden = false;
        });

        document.querySelectorAll('[data-preview-clear="' + box.id + '"]').forEach(function (btn) {
            btn.addEventListener("click", function () {
                input.value = "";   // form se file bhi hata do, sirf preview nahi
                clear();
            });
        });
    });

    // ---------- 5. TIMESTAMPS IN THE READER'S TIMEZONE ----------
    // Server UTC bhejta hai (data-utc). Server ka timezone user ka timezone nahi
    // hota, is liye asli waqt browser hi theek bata sakta hai. JS band ho to
    // server ka rendered title reh jata hai — koi cheez tooti nahi.
    document.querySelectorAll("time[data-utc]").forEach(function (el) {
        var when = new Date(el.getAttribute("data-utc"));
        if (isNaN(when)) return;
        el.title = when.toLocaleString(undefined, {
            day: "numeric", month: "short", year: "numeric",
            hour: "numeric", minute: "2-digit"
        });
    });

    // ---------- 6. <details> MENUS: CLOSE ON OUTSIDE CLICK / ESCAPE ----------
    // <details> khud hi keyboard aur screen readers ke liye sahi kaam karta hai.
    // Sirf ek cheez missing hai jo log dropdown se expect karte hain: bahar
    // click karne par band ho jana.
    document.addEventListener("click", function (e) {
        document.querySelectorAll("details[data-menu][open]").forEach(function (d) {
            if (!d.contains(e.target)) d.removeAttribute("open");
        });
    });

    document.addEventListener("keydown", function (e) {
        if (e.key !== "Escape") return;
        document.querySelectorAll("details[data-menu][open]").forEach(function (d) {
            d.removeAttribute("open");
            var summary = d.querySelector("summary");
            if (summary) summary.focus();   // focus wapas trigger par
        });
    });

    // ---------- 7. CONFIRM DESTRUCTIVE ACTIONS ----------
    // Post delete wapas nahi aati, is liye ek confirm. JS band ho to form phir
    // bhi chalta hai — sirf confirm nahi aata.
    document.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-confirm]");
        if (!btn) return;
        if (!window.confirm(btn.getAttribute("data-confirm"))) e.preventDefault();
    });

    // ---------- 8. "NEW POST" JUMPS INTO THE COMPOSER ----------
    // Sidebar/mobile ka New post button Home par #composer bhejta hai. Modal
    // banane ke bajaye seedha textarea focus karna kaafi hai — aur back button
    // bhi normal rehta hai.
    if (window.location.hash === "#composer") {
        var field = document.getElementById("Content");
        if (field) {
            field.focus();
            // Focus scroll kar deta hai magar sticky header ke neeche na chhupe.
            // prefers-reduced-motion ka ehtiram: JS ka "smooth" CSS ki media
            // query khud nahi maanta, is liye yahan haath se check kar rahe hain.
            var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            field.scrollIntoView({ block: "center", behavior: reduceMotion ? "auto" : "smooth" });
        }
    }

    // ---------- 9. LIKE / FOLLOW WITHOUT A PAGE RELOAD ----------
    // Progressive enhancement: HTML mein ye poore <form> hain jo JS ke bina bhi
    // POST ho jate hain. Yahan hum unhein rok kar fetch se bhejte hain, sirf is
    // liye ke like karne par scroll position aur feed ka page zaya na ho —
    // Instagram par like karte hi feed hil jane wali dikkat.
    //
    // FormData mein antiforgery token khud shamil ho jata hai (form tag helper
    // usse hidden field ke tor par render karta hai), is liye alag se header
    // bhejne ki zaroorat nahi.
    function submitInBackground(form) {
        return fetch(form.action, {
            method: "POST",
            body: new FormData(form),
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        }).then(function (res) {
            if (!res.ok) throw new Error(res.status);
            return res.json();
        });
    }

    function wireToggle(selector, apply) {
        document.addEventListener("submit", function (e) {
            var form = e.target.closest(selector);
            if (!form) return;

            e.preventDefault();

            var btn = form.querySelector("button[type=submit]");
            // Double-click se do requests na jayen. Server unique index se bacha
            // leta hai, magar UI do dafa palat kar confuse karta hai.
            if (!btn || btn.disabled) return;
            btn.disabled = true;

            submitInBackground(form)
                .then(function (data) { apply(form, btn, data); })
                .catch(function () {
                    // Network ya server ka masla: chupana nahi. Normal form POST
                    // kara do — page reload hoga magar user ka kaam ho jayega.
                    form.submit();
                })
                .then(function () { btn.disabled = false; });
        });
    }

    wireToggle("form[data-like-form]", function (form, btn, data) {
        var liked = !!data.liked;
        btn.classList.toggle("is-liked", liked);
        btn.setAttribute("aria-pressed", liked ? "true" : "false");
        btn.title = liked ? "Unlike this post" : "Like this post";

        var icon = form.querySelector("[data-like-icon]");
        if (icon) icon.textContent = liked ? "♥" : "♡";

        var count = form.querySelector("[data-like-count]");
        if (count) count.textContent = data.count;
    });

    wireToggle("form[data-follow-form]", function (form, btn, data) {
        var following = !!data.following;
        btn.classList.toggle("is-following", following);
        btn.classList.toggle("btn-primary", !following);
        btn.setAttribute("aria-pressed", following ? "true" : "false");

        var label = form.querySelector("[data-follow-label]");
        if (label) label.textContent = following ? "Following" : "Follow";

        // Profile ya UserCard ka Followers count bhi wahin update — card ke context
        // mein dhoondo taake search list mein doosron ke cards kharab na hon.
        var cardScope = form.closest(".user-card, .profile-head");
        var followers = cardScope
            ? cardScope.querySelector("[data-follower-count]")
            : document.querySelector("[data-follower-count]");
        if (followers) followers.textContent = data.followerCount;
    });
})();
