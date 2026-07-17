const express = require('express');
const nodemailer = require('nodemailer');
const cors = require('cors');

const app = express();
app.use(express.json());
app.use(cors({ origin: ['https://tagalong.delivery', 'https://www.tagalong.delivery'] }));

const transporter = nodemailer.createTransport({
  host: process.env.SMTP_HOST || 'smtp.hostinger.com',
  port: parseInt(process.env.SMTP_PORT || '465'),
  secure: true,
  auth: {
    user: process.env.SMTP_USER || 'info@tlimc.net',
    pass: process.env.SMTP_PASS,
  },
});

app.post('/waitlist', async (req, res) => {
  const { email } = req.body;
  if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    return res.status(400).json({ error: 'Invalid email' });
  }

  try {
    // Notify admin
    await transporter.sendMail({
      from: '"TagAlong Waitlist" <info@tlimc.net>',
      to: 'info@tlimc.net',
      subject: `New Waitlist Signup: ${email}`,
      html: `
        <h2>New TagAlong Waitlist Signup</h2>
        <p><strong>Email:</strong> ${email}</p>
        <p><strong>Time:</strong> ${new Date().toUTCString()}</p>
        <hr/>
        <p style="color:#888;font-size:12px">Sent from tagalong.delivery waitlist form</p>
      `,
    });

    // Confirmation to the subscriber
    await transporter.sendMail({
      from: '"TagAlong" <info@tlimc.net>',
      to: email,
      subject: 'You\'re on the TagAlong Waitlist!',
      html: `<!DOCTYPE html>
<html lang="en">
<head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;">
  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;padding:40px 0;">
    <tr><td align="center">
      <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

        <!-- Header -->
        <tr>
          <td style="background:linear-gradient(135deg,#FF6B00 0%,#e55a00 100%);padding:40px 32px;text-align:center;">
            <h1 style="margin:0;color:#ffffff;font-size:32px;font-weight:800;letter-spacing:-1px;">Tag<span style="color:#fff3e0;">Along</span></h1>
            <p style="margin:8px 0 0;color:#ffe0cc;font-size:14px;letter-spacing:1px;text-transform:uppercase;">Travel Smart. Deliver Easy.</p>
          </td>
        </tr>

        <!-- Body -->
        <tr>
          <td style="padding:40px 32px;">
            <h2 style="margin:0 0 12px;color:#1a1a1a;font-size:24px;font-weight:700;">You're on the list! 🎉</h2>
            <p style="margin:0 0 24px;color:#555;font-size:16px;line-height:1.6;">
              Thank you for joining the TagAlong waitlist. We've received your details and we'll be in touch soon with everything you need to know about our launch.
            </p>

            <!-- Divider card -->
            <table width="100%" cellpadding="0" cellspacing="0" style="background:#fff8f3;border-left:4px solid #FF6B00;border-radius:8px;margin-bottom:28px;">
              <tr><td style="padding:20px 24px;">
                <p style="margin:0;color:#FF6B00;font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:1px;">What's coming</p>
                <ul style="margin:10px 0 0;padding-left:18px;color:#444;font-size:15px;line-height:1.8;">
                  <li>Find affordable rides with travellers on your route</li>
                  <li>Send packages easily — no courier needed</li>
                  <li>Earn money carrying packages on trips you're already taking</li>
                  <li>Real-time tracking &amp; in-app messaging</li>
                </ul>
              </td></tr>
            </table>

            <p style="margin:0 0 32px;color:#555;font-size:15px;line-height:1.6;">
              We're launching very soon. Stay tuned — we'll send you early access details before anyone else.
            </p>

            <!-- CTA -->
            <table cellpadding="0" cellspacing="0" style="margin:0 auto 32px;">
              <tr><td style="background:#FF6B00;border-radius:50px;text-align:center;">
                <a href="https://tagalong.delivery" style="display:inline-block;padding:14px 36px;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;letter-spacing:0.3px;">Visit TagAlong →</a>
              </td></tr>
            </table>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style="background:#1a1a1a;padding:24px 32px;text-align:center;">
            <p style="margin:0 0 8px;color:#aaa;font-size:13px;">© ${new Date().getFullYear()} TagAlong. All rights reserved.</p>
            <p style="margin:0;color:#666;font-size:12px;">You received this email because you signed up at <a href="https://tagalong.delivery" style="color:#FF6B00;text-decoration:none;">tagalong.delivery</a></p>
          </td>
        </tr>

      </table>
    </td></tr>
  </table>
</body>
</html>`,
    });

    res.json({ success: true });
  } catch (err) {
    console.error('Mail error:', err.message);
    res.status(500).json({ error: 'Failed to send email' });
  }
});

app.get('/health', (_, res) => res.json({ ok: true }));

const PORT = process.env.PORT || 3001;
app.listen(PORT, () => console.log(`Mail service running on port ${PORT}`));
