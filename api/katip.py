from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request
from datetime import datetime
import pytz # Türkiye saati için

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            user_text = body.get('text', '')
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()

            # Türkiye Saatini Otomatik Alıyoruz
            tr_timezone = pytz.timezone('Europe/Istanbul')
            su_an = datetime.now(tr_timezone).strftime("%d %B %Y %A, Saat: %H:%M")

            model_name = "gemini-3-flash-preview" 
            url = f"https://generativelanguage.googleapis.com/v1beta/models/{model_name}:generateContent?key={api_key}"
            
            # Gemini'a gerçek zamanı her seferinde biz söylüyoruz
            sistem_talimati = (
                f"Sen Gemini 3 Flash sürümüsün. Güncel tarih ve saat bilgisi: {su_an}. "
                "Cevaplarını her zaman bu tarihe göre ver. Kendi iç düşüncelerini paylaşma, "
                "doğrudan kullanıcıyla konuş."
            )
            
            payload = {
                "contents": [{"parts": [{"text": f"{sistem_talimati}\n\nKullanıcı: {user_text}"}]}]
            }
            
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            
            with urllib.request.urlopen(req, timeout=15) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
            
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))

        except Exception as e:
            self.send_response(500)
            self.end_headers()
            self.wfile.write(json.dumps({"error": str(e)}).encode('utf-8'))
