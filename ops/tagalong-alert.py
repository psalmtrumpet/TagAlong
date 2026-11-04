#!/usr/bin/env python3
"""Email an alert about the server, through TagAlong's own mailbox (the
Email__* settings in /opt/tagalong/.env). Usage: tagalong-alert.py SUBJECT BODY"""
import smtplib
import ssl
import sys
from email.message import EmailMessage

ENV = "/opt/tagalong/.env"
TO = "info@tagalong.delivery"


def settings():
    values = {}
    for line in open(ENV, encoding="utf-8"):
        if "=" in line and not line.lstrip().startswith("#"):
            k, v = line.rstrip("\n").split("=", 1)
            values[k.strip()] = v.strip().strip('"')
    return values


def main():
    subject, body = sys.argv[1], sys.argv[2]
    env = settings()
    host = env.get("Email__SmtpHost") or "smtp.hostinger.com"
    port = int(env.get("Email__SmtpPort") or 587)
    user, password = env.get("Email__SmtpUser", ""), env.get("Email__SmtpPass", "")
    sender = env.get("Email__FromAddress") or user

    msg = EmailMessage()
    msg["Subject"] = f"[TagAlong server] {subject}"
    msg["From"] = f"TagAlong server <{sender}>"
    msg["To"] = TO
    msg.set_content(body)

    context = ssl.create_default_context()
    if port == 465:
        with smtplib.SMTP_SSL(host, port, context=context, timeout=20) as s:
            s.login(user, password)
            s.send_message(msg)
    else:
        with smtplib.SMTP(host, port, timeout=20) as s:
            s.starttls(context=context)
            s.login(user, password)
            s.send_message(msg)


if __name__ == "__main__":
    main()
