/* =====================================================
   Web Builders — Domain search, cart & card checkout
   Talks to the .NET Domains API (src/) and Stripe.js.
   ===================================================== */
(function () {
  'use strict';

  const $ = (sel) => document.querySelector(sel);

  /* ---------- API ---------- */
  const apiBase = (() => {
    const meta = document.querySelector('meta[name="domains-api"]')?.content?.trim();
    if (meta) return meta.replace(/\/$/, '');
    // Opened from disk or VS Code Live Server: point at the local API.
    if (location.protocol === 'file:' || location.port === '5500') return 'http://localhost:5080';
    return '';
  })();

  class ApiError extends Error {
    constructor(message, status, errors) {
      super(message);
      this.status = status;
      this.errors = errors || {};
    }
  }

  async function api(path, options = {}) {
    let res;
    try {
      res = await fetch(apiBase + path, {
        ...options,
        headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
      });
    } catch {
      throw new ApiError('The domain service could not be reached. Is the API running?', 0);
    }
    const body = await res.json().catch(() => null);
    if (!res.ok) {
      throw new ApiError(body?.detail || body?.title || `Request failed (${res.status})`, res.status, body?.errors);
    }
    return body;
  }

  /* ---------- State ---------- */
  const CART_KEY = 'wb-domain-cart';
  const state = {
    config: null,
    results: [],
    cart: loadCart(),
    stripe: null,
    card: null,
    intent: null, // { paymentIntentId, clientSecret, signature }
  };

  function loadCart() {
    try {
      const saved = JSON.parse(sessionStorage.getItem(CART_KEY) || '[]');
      return Array.isArray(saved) ? saved : [];
    } catch {
      return [];
    }
  }

  function saveCart() {
    try {
      sessionStorage.setItem(CART_KEY, JSON.stringify(state.cart));
    } catch {
      /* storage unavailable: cart just won't survive a reload */
    }
  }

  const cartSignature = () => state.cart.map((i) => `${i.domain}:${i.years}`).sort().join(',');

  /* ---------- Formatting ---------- */
  function money(cents) {
    const currency = (state.config?.currency || 'usd').toUpperCase();
    return new Intl.NumberFormat(undefined, { style: 'currency', currency, currencyDisplay: 'narrowSymbol' }).format(cents / 100);
  }

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, (c) =>
      ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]
    );
  }

  function setBusy(button, busy) {
    button.disabled = busy;
    button.querySelector('.spinner-border')?.classList.toggle('d-none', !busy);
    button.querySelector('.btn-label')?.classList.toggle('opacity-50', busy);
  }

  function showAlert(message, kind = 'error') {
    const el = $('#domainAlert');
    if (!message) {
      el.hidden = true;
      return;
    }
    el.className = `domain-alert domain-alert-${kind}`;
    el.innerHTML = `<i class="bi ${kind === 'error' ? 'bi-exclamation-triangle' : 'bi-info-circle'}"></i><div></div>`;
    el.querySelector('div').textContent = message;
    el.hidden = false;
  }

  function showPanel(name) {
    $('#resultsPanel').hidden = name !== 'results';
    $('#checkoutPanel').hidden = name !== 'checkout';
    $('#confirmPanel').hidden = name !== 'confirm';
    $('#cart').classList.toggle('is-locked', name !== 'results');
  }

  /* ---------- Config ---------- */
  async function loadConfig() {
    try {
      state.config = await api('/api/store/config');
    } catch (err) {
      showAlert(`${err.message} Start it with "dotnet run" in src/WebBuilders.Domains.Api (see docs/local-development.md).`);
      return;
    }

    $('#tldStrip').innerHTML = state.config.tlds
      .map((t) => `<span class="domain-tld-chip"><b>.${escapeHtml(t.tld)}</b> ${money(t.yearlyPriceCents)}</span>`)
      .join('');

    const notice = $('#modeNotice');
    const registrar = state.config.liveRegistrar
      ? `Registrar: <b>${escapeHtml(state.config.registrar)}</b>`
      : `Availability from the public registry · registrations are <b>simulated</b>`;
    const payments = state.config.paymentsEnabled
      ? 'Payments: <b>Stripe test mode</b>'
      : 'Payments: <b>not configured</b>';
    notice.innerHTML = `<i class="bi bi-flask"></i> Sandbox · ${registrar} · ${payments}`;
    notice.hidden = false;

    renderCart();
  }

  /* ---------- Search ---------- */
  async function search(query) {
    const button = $('#domainSearchBtn');
    showAlert(null);
    showPanel('results');
    setBusy(button, true);
    $('#resultsEmpty').hidden = true;
    $('#resultsList').innerHTML = Array.from({ length: 4 }, () => '<div class="domain-row is-loading"></div>').join('');

    try {
      const data = await api(`/api/domains/search?q=${encodeURIComponent(query)}`);
      state.results = data.results;
      renderResults();
      const url = new URL(location.href);
      url.searchParams.set('q', data.query);
      history.replaceState(null, '', url);
    } catch (err) {
      $('#resultsList').innerHTML = '';
      $('#resultsEmpty').hidden = false;
      showAlert(err.message);
    } finally {
      setBusy(button, false);
    }
  }

  const STATUS_LABEL = { available: 'Available', taken: 'Taken', unknown: 'Unknown' };

  function renderResults() {
    const inCart = new Set(state.cart.map((i) => i.domain));
    $('#resultsList').innerHTML = state.results
      .map((r, index) => {
        const [label, ...rest] = r.domain.split('.');
        const added = inCart.has(r.domain);
        const action =
          r.status === 'available'
            ? `<button type="button" class="btn ${added ? 'btn-light-custom' : 'btn-primary'} btn-sm-custom" data-toggle-cart="${escapeHtml(r.domain)}">
                 ${added ? '<i class="bi bi-check2"></i> In cart' : '<i class="bi bi-plus-lg"></i> Add'}
               </button>`
            : `<button type="button" class="btn btn-light-custom btn-sm-custom" disabled>Unavailable</button>`;

        return `
          <div class="domain-row ${index === 0 ? 'is-featured' : ''} is-${r.status}">
            <div class="domain-row-name">
              ${index === 0 ? '<span class="domain-row-flag">Your search</span>' : ''}
              <span class="domain-row-domain">${escapeHtml(label)}<span>.${escapeHtml(rest.join('.'))}</span></span>
              ${r.note ? `<span class="domain-row-note">${escapeHtml(r.note)}</span>` : ''}
            </div>
            <span class="domain-status domain-status-${r.status}">${STATUS_LABEL[r.status] || r.status}</span>
            <div class="domain-row-price">${money(r.yearlyPriceCents)}<small>/yr</small></div>
            <div class="domain-row-action">${action}</div>
          </div>`;
      })
      .join('');
  }

  /* ---------- Cart ---------- */
  function toggleCart(domain) {
    const existing = state.cart.findIndex((i) => i.domain === domain);
    if (existing >= 0) {
      state.cart.splice(existing, 1);
    } else {
      const result = state.results.find((r) => r.domain === domain);
      if (!result || result.status !== 'available') return;
      if (state.cart.length >= 10) {
        showAlert('You can buy up to 10 domains per order.');
        return;
      }
      state.cart.push({ domain, years: 1, yearlyPriceCents: result.yearlyPriceCents });
    }
    saveCart();
    renderCart();
    renderResults();
  }

  function renderCart() {
    const total = state.cart.reduce((sum, i) => sum + i.yearlyPriceCents * i.years, 0);
    $('#cartCount').textContent = state.cart.length;
    $('#cartEmpty').hidden = state.cart.length > 0;
    $('#cartTotalRow').hidden = state.cart.length === 0;
    $('#checkoutBtn').hidden = state.cart.length === 0;
    $('#cartTotal').textContent = money(total);
    $('#payAmount').textContent = money(total);

    $('#cartList').innerHTML = state.cart
      .map(
        (i) => `
        <li>
          <div class="domain-cart-item">
            <span class="domain-cart-name">${escapeHtml(i.domain)}</span>
            <button type="button" class="domain-cart-remove" data-remove="${escapeHtml(i.domain)}" aria-label="Remove ${escapeHtml(i.domain)}">
              <i class="bi bi-x-lg"></i>
            </button>
          </div>
          <div class="domain-cart-meta">
            <select class="form-select form-select-sm" data-years="${escapeHtml(i.domain)}" aria-label="Registration period for ${escapeHtml(i.domain)}">
              ${[1, 2, 3, 5, 10]
                .map((y) => `<option value="${y}" ${y === i.years ? 'selected' : ''}>${y} year${y > 1 ? 's' : ''}</option>`)
                .join('')}
            </select>
            <span>${money(i.yearlyPriceCents * i.years)}</span>
          </div>
        </li>`
      )
      .join('');
  }

  /* ---------- Stripe ---------- */
  function loadStripeJs() {
    if (window.Stripe) return Promise.resolve();
    return new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'https://js.stripe.com/v3/';
      script.onload = resolve;
      script.onerror = () => reject(new Error('Could not load the secure card form.'));
      document.head.appendChild(script);
    });
  }

  async function mountCardForm() {
    if (state.card) return;
    await loadStripeJs();
    state.stripe = window.Stripe(state.config.stripePublishableKey);

    const elements = state.stripe.elements({
      fonts: [{ cssSrc: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500&display=swap' }],
    });
    const style = {
      base: {
        fontFamily: 'Inter, sans-serif',
        fontSize: '15px',
        color: '#0B1437',
        '::placeholder': { color: '#98A2B3' },
      },
      invalid: { color: '#DC3545' },
    };

    const number = elements.create('cardNumber', { style, showIcon: true });
    const expiry = elements.create('cardExpiry', { style });
    const cvc = elements.create('cardCvc', { style });
    number.mount('#cardNumber');
    expiry.mount('#cardExpiry');
    cvc.mount('#cardCvc');

    [number, expiry, cvc].forEach((el) => {
      el.on('change', (e) => ($('#cardError').textContent = e.error ? e.error.message : ''));
    });
    state.card = number;
  }

  async function openCheckout() {
    showAlert(null);
    if (!state.config) return showAlert('The domain service is offline.');
    if (!state.config.paymentsEnabled) {
      return showAlert('Card payments are not configured on the server yet. Add Stripe test keys (see docs/local-development.md).');
    }
    showPanel('checkout');
    window.scrollTo({ top: $('.domain-section').offsetTop - 80, behavior: 'smooth' });
    try {
      await mountCardForm();
    } catch (err) {
      showAlert(err.message);
    }
  }

  /* ---------- Checkout ---------- */
  const CONTACT_FIELDS = ['firstName', 'lastName', 'email', 'phone', 'address1', 'city', 'stateProvince', 'postalCode', 'country'];

  function readContact() {
    return Object.fromEntries(CONTACT_FIELDS.map((f) => [f, $('#' + f).value.trim()]));
  }

  function setFieldError(field, message) {
    const input = $('#' + field);
    if (!input) return;
    input.classList.toggle('is-invalid', !!message);
    const feedback = input.parentElement.querySelector('.invalid-feedback');
    if (feedback) feedback.textContent = message || '';
  }

  function validateContact(contact) {
    let ok = true;
    CONTACT_FIELDS.forEach((f) => {
      const input = $('#' + f);
      const message = !contact[f] ? 'Required.' : input.type === 'email' && !input.checkValidity() ? 'Enter a valid email.' : '';
      setFieldError(f, message);
      if (message) ok = false;
    });
    return ok;
  }

  async function ensurePaymentIntent() {
    const signature = cartSignature();
    if (state.intent && state.intent.signature === signature) return state.intent;

    const session = await api('/api/checkout/payment-intents', {
      method: 'POST',
      body: JSON.stringify({ items: state.cart.map(({ domain, years }) => ({ domain, years })) }),
    });
    state.intent = { ...session, signature };
    $('#payAmount').textContent = money(session.amountCents);
    return state.intent;
  }

  async function completeOrder(paymentIntentId, contact) {
    const order = await api('/api/orders', {
      method: 'POST',
      body: JSON.stringify({ paymentIntentId, contact }),
    });
    state.cart = [];
    state.intent = null;
    saveCart();
    renderCart();
    renderConfirmation(order);
  }

  async function pay(event) {
    event.preventDefault();
    showAlert(null);
    $('#cardError').textContent = '';

    const contact = readContact();
    if (!validateContact(contact)) {
      document.querySelector('#checkoutForm .is-invalid')?.focus();
      return;
    }

    const button = $('#payBtn');
    setBusy(button, true);
    let paidIntentId = null;

    try {
      const intent = await ensurePaymentIntent();

      const { error, paymentIntent } = await state.stripe.confirmCardPayment(intent.clientSecret, {
        payment_method: {
          card: state.card,
          billing_details: {
            name: `${contact.firstName} ${contact.lastName}`,
            email: contact.email,
            phone: contact.phone,
            address: {
              line1: contact.address1,
              city: contact.city,
              state: contact.stateProvince,
              postal_code: contact.postalCode,
              country: contact.country,
            },
          },
        },
      });

      if (error) {
        $('#cardError').textContent = error.message;
        return;
      }
      if (paymentIntent.status !== 'succeeded') {
        $('#cardError').textContent = 'The payment was not completed. Please try again.';
        return;
      }

      paidIntentId = paymentIntent.id;
      await completeOrder(paidIntentId, contact);
    } catch (err) {
      Object.entries(err.errors || {}).forEach(([field, messages]) =>
        setFieldError(field.charAt(0).toLowerCase() + field.slice(1), [].concat(messages)[0])
      );
      showAlert(
        paidIntentId
          ? `Your card was charged but we could not finish the order: ${err.message} Press Pay again to retry — you will not be charged twice.`
          : err.message
      );
      if (paidIntentId) retryAfterPayment(paidIntentId);
    } finally {
      setBusy(button, false);
    }
  }

  // After a successful charge, "Pay" re-submits the order instead of charging again.
  function retryAfterPayment(paymentIntentId) {
    const form = $('#checkoutForm');
    form.removeEventListener('submit', pay);
    form.addEventListener('submit', async function retry(e) {
      e.preventDefault();
      const contact = readContact();
      if (!validateContact(contact)) return;
      setBusy($('#payBtn'), true);
      try {
        await completeOrder(paymentIntentId, contact);
        form.removeEventListener('submit', retry);
        form.addEventListener('submit', pay);
        showAlert(null);
      } catch (err) {
        showAlert(`Still could not finish the order: ${err.message}`);
      } finally {
        setBusy($('#payBtn'), false);
      }
    });
  }

  function renderConfirmation(order) {
    const ok = order.lines.filter((l) => l.registered);
    const failed = order.lines.filter((l) => !l.registered);

    $('#confirmTitle').textContent = failed.length === 0 ? 'Your order is complete' : 'Your order is partly complete';
    $('#confirmSummary').textContent =
      `Charged ${money(order.chargedCents)}` +
      (order.refundedCents ? `, refunded ${money(order.refundedCents)}` : '') +
      ` · Payment ${order.paymentIntentId}` +
      (order.liveRegistrar ? '' : ' · registrations simulated');

    $('#confirmList').innerHTML = order.lines
      .map(
        (l) => `
        <li class="${l.registered ? 'is-ok' : 'is-failed'}">
          <i class="bi ${l.registered ? 'bi-check-circle-fill' : 'bi-x-circle-fill'}"></i>
          <div>
            <strong>${escapeHtml(l.domain)}</strong> · ${l.years} year${l.years > 1 ? 's' : ''} · ${money(l.priceCents)}
            <small>${
              l.registered
                ? `Order ${escapeHtml(l.registrarOrderId || '—')}${l.message ? ' · ' + escapeHtml(l.message) : ''}`
                : `${escapeHtml(l.message || 'Registration failed')}${l.refunded ? ' · refunded' : ' · refund pending, we will contact you'}`
            }</small>
          </div>
        </li>`
      )
      .join('');

    showPanel('confirm');
    if (ok.length) showAlert(null);
  }

  /* ---------- Events ---------- */
  $('#domainSearchForm').addEventListener('submit', (e) => {
    e.preventDefault();
    const query = $('#domainQuery').value.trim();
    if (!query) {
      $('#domainQuery').focus();
      return;
    }
    search(query);
  });

  $('#resultsList').addEventListener('click', (e) => {
    const button = e.target.closest('[data-toggle-cart]');
    if (button) toggleCart(button.dataset.toggleCart);
  });

  $('#cartList').addEventListener('click', (e) => {
    const button = e.target.closest('[data-remove]');
    if (!button || $('#cart').classList.contains('is-locked')) return;
    state.cart = state.cart.filter((i) => i.domain !== button.dataset.remove);
    saveCart();
    renderCart();
    renderResults();
  });

  $('#cartList').addEventListener('change', (e) => {
    const select = e.target.closest('[data-years]');
    if (!select) return;
    const item = state.cart.find((i) => i.domain === select.dataset.years);
    if (item) item.years = Number(select.value);
    saveCart();
    renderCart();
  });

  $('#checkoutBtn').addEventListener('click', openCheckout);
  $('#backToResults').addEventListener('click', () => showPanel('results'));
  $('#checkoutForm').addEventListener('submit', pay);
  $('#checkoutForm').addEventListener('input', (e) => e.target.classList.contains('is-invalid') && setFieldError(e.target.id, ''));
  $('#newSearchBtn').addEventListener('click', () => {
    showPanel('results');
    $('#domainQuery').focus();
  });

  /* ---------- Boot ---------- */
  renderCart();
  loadConfig().then(() => {
    const q = new URLSearchParams(location.search).get('q');
    if (q && state.config) {
      $('#domainQuery').value = q;
      search(q);
    }
  });
})();
