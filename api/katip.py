from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request
from datetime import datetime
import pytz

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            
            user_text = body.get('text', '')
            image_data = body.get('image', None)
            selected_model = body.get('model', 'gemini-3-flash-preview')
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()

            tr_timezone = pytz.timezone('Europe/Istanbul')
            su_an = datetime.now(tr_timezone).strftime("%d %B %Y %A, %H:%M")

            url = f"https://generativelanguage.googleapis.com/v1beta/models/{selected_model}:generateContent?key={api_key}"
            
            sistem_talimati = f"Bugün: {su_an}. Sen profesyonel bir asistansın. Cevapların net olsun."
            
            parts = [{"text": f"{sistem_talimati}\n\nSoru: {user_text}"}]
            if image_data:
                parts.append({"inline_data": {"mime_type": "image/jpeg", "data": image_data.split(",")[-1]}})

            payload = {"contents": [{"parts": parts}], "tools": [{"google_search_retrieval": {}}]}
            
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            
            # Timeout'u 25 saniyeye çıkardık ama Vercel 10'da kesebilir
            with urllib.request.urlopen(req, timeout=25) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                
                if 'candidates' in gemini_json:
                    ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
                else:
                    ai_cevabi = "Üzgünüm, model şu an cevap üretemedi."
            
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))

        except Exception as e:
            self.send_response(200) # Hatayı 200 ile gönderip JS'de yakalayalım
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            error_msg = "Derin düşünce çok uzun sürdüğü için Vercel bağlantıyı kesti. Lütfen Flash modelini deneyin veya daha kısa bir soru sorun." if "timeout" in str(e).lower() else f"Hata: {str(e)}"
            self.wfile.write(json.dumps({"result": error_msg}).encode('utf-8'))
