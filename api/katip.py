from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request
import base64
from datetime import datetime
import pytz

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            
            user_text = body.get('text', '')
            image_data = body.get('image', None) # Base64 formatında fotoğraf
            selected_model = body.get('model', 'gemini-3-flash-preview')
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()

            tr_timezone = pytz.timezone('Europe/Istanbul')
            su_an = datetime.now(tr_timezone).strftime("%d %B %Y %A, %H:%M")

            url = f"https://generativelanguage.googleapis.com/v1beta/models/{selected_model}:generateContent?key={api_key}"
            
            sistem_talimati = f"Sen en güncel Gemini sürümüsün. Tarih: {su_an}. Kullanıcıya her konuda yardımcı ol."
            
            # İçerik yapısını oluştur (Multimodal destekli)
            parts = [{"text": f"{sistem_talimati}\n\nKullanıcı: {user_text}"}]
            
            if image_data:
                # Fotoğraf varsa listeye ekle
                parts.append({
                    "inline_data": {
                        "mime_type": "image/jpeg",
                        "data": image_data.split(",")[-1] # Base64 kısmını ayıkla
                    }
                })

            payload = {"contents": [{"parts": parts}]}
            
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            
            with urllib.request.urlopen(req, timeout=30) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
            
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))

        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": str(e)}).encode('utf-8'))
