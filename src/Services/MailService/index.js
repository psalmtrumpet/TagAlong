'use strict';
const express    = require('express');
const nodemailer = require('nodemailer');
const fs         = require('fs');
const path       = require('path');

const app      = express();
const PORT     = parseInt(process.env.PORT || '3001', 10);
const DATA_DIR = process.env.DATA_DIR || '/data';
const DATA_FILE = path.join(DATA_DIR, 'waitlist.json');
const ADMIN_KEY = process.env.ADMIN_KEY || '';

app.use(express.json({ limit: '16kb' }));

// ── bootstrap data file ──────────────────────────────────────────────────────
if (!fs.existsSync(DATA_DIR)) fs.mkdirSync(DATA_DIR, { recursive: true });
if (!fs.existsSync(DATA_FILE)) fs.writeFileSync(DATA_FILE, '[]', 'utf8');

// ── SMTP transporter ─────────────────────────────────────────────────────────
const transporter = nodemailer.createTransport({
  host:   process.env.SMTP_HOST || 'smtp.hostinger.com',
  port:   parseInt(process.env.SMTP_PORT || '465', 10),
  secure: (process.env.SMTP_PORT || '465') === '465',
  auth: {
    user: process.env.SMTP_USER,
    pass: process.env.SMTP_PASS,
  },
});

// ── helpers ──────────────────────────────────────────────────────────────────
function readEntries() {
  try { return JSON.parse(fs.readFileSync(DATA_FILE, 'utf8')); }
  catch { return []; }
}

function writeEntries(entries) {
  fs.writeFileSync(DATA_FILE, JSON.stringify(entries, null, 2), 'utf8');
}

function ackEmail(name) {
  const firstName = name.split(' ')[0];
  return `
<!DOCTYPE html>
<html lang="en">
<head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>You're on the list!</title>
<style>
  body{margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
  .wrap{max-width:560px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08)}
  .hero{background:#FF6B00;padding:36px 40px;text-align:center}
  .hero img{height:36px;margin-bottom:16px;display:block;margin-left:auto;margin-right:auto}
  .hero h1{margin:0;font-size:22px;font-weight:700;color:#fff;letter-spacing:-.3px}
  .hero p{margin:8px 0 0;font-size:14px;color:rgba(255,255,255,.85)}
  .body{padding:36px 40px}
  .body p{margin:0 0 16px;font-size:15px;line-height:1.6;color:#333}
  .badge{display:inline-block;background:#fff3e8;border:1px solid #ffd9bb;color:#c04f00;font-size:13px;font-weight:600;padding:8px 18px;border-radius:20px;margin:8px 0 20px}
  .footer{padding:20px 40px;background:#fafafa;border-top:1px solid #eee;text-align:center;font-size:12px;color:#999}
  .footer a{color:#FF6B00;text-decoration:none}
  @media(max-width:600px){.hero,.body,.footer{padding:28px 24px}}
</style></head>
<body>
<div class="wrap">
  <div class="hero">
    <h1>You're on the list, ${firstName}! 🎉</h1>
    <p>TagAlong is launching soon — we'll let you know first.</p>
  </div>
  <div class="body">
    <p>Hi ${firstName},</p>
    <p>Thanks for joining the TagAlong waitlist! You're among the first to hear about our launch.</p>
    <div style="text-align:center">
      <span class="badge">✅ Waitlist confirmed</span>
    </div>
    <p>TagAlong connects travellers and senders — share rides intercity, send packages affordably, or earn on your next trip. When we launch in your city, you'll be the first to know.</p>
    <p>Stay tuned — we're moving fast.</p>
    <p style="margin-bottom:0">The TagAlong Team</p>
  </div>
  <div class="footer">
    You received this because you signed up at <a href="https://tagalong.delivery">tagalong.delivery</a>.<br>
    &copy; ${new Date().getFullYear()} TagAlong. All rights reserved.
  </div>
</div>
</body></html>`.trim();
}

// ── POST /waitlist ────────────────────────────────────────────────────────────
app.post('/waitlist', async (req, res) => {
  const { name, email, phone } = req.body || {};

  if (!name || typeof name !== 'string' || name.trim().length < 2)
    return res.status(400).json({ error: 'A valid name is required' });
  if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email))
    return res.status(400).json({ error: 'A valid email is required' });

  const cleanName  = name.trim().slice(0, 100);
  const cleanEmail = email.trim().toLowerCase().slice(0, 254);
  const cleanPhone = typeof phone === 'string' ? phone.trim().slice(0, 20) : '';

  // Persist entry (deduplicate by email)
  const entries = readEntries();
  const already = entries.some(e => e.email === cleanEmail);
  if (!already) {
    entries.push({ name: cleanName, email: cleanEmail, phone: cleanPhone, joinedAt: new Date().toISOString() });
    writeEntries(entries);
  }

  // Send acknowledgement email (don't block the response on failure)
  if (process.env.SMTP_USER && process.env.SMTP_PASS) {
    transporter.sendMail({
      from:    `"TagAlong" <${process.env.SMTP_USER}>`,
      to:      `"${cleanName}" <${cleanEmail}>`,
      subject: "You're on the TagAlong waitlist!",
      html:    ackEmail(cleanName),
    }).catch(err => console.error('[mail] Failed to send ack email:', err.message));
  }

  res.json({ message: already ? 'Already on the waitlist' : 'Successfully joined the waitlist' });
});

// ── GET /waitlist (admin — requires ADMIN_KEY header) ──────────────────────
app.get('/waitlist', (req, res) => {
  if (!ADMIN_KEY || req.headers['x-admin-key'] !== ADMIN_KEY)
    return res.status(401).json({ error: 'Unauthorized' });
  res.json(readEntries());
});

app.listen(PORT, () => console.log(`[mail-service] Listening on port ${PORT}`));
