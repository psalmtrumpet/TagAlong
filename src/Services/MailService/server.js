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
    res.json({ success: true });
  } catch (err) {
    console.error('Mail error:', err.message);
    res.status(500).json({ error: 'Failed to send email' });
  }
});

app.get('/health', (_, res) => res.json({ ok: true }));

const PORT = process.env.PORT || 3001;
app.listen(PORT, () => console.log(`Mail service running on port ${PORT}`));
