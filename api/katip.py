import json
import os
import urllib.request
from http.server import BaseHTTPRequestHandler
from datetime import datetime
import pytz

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            
            user_text = body.get('text', '')
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()

            # Tarih ve saat hesaplaması
            tr_timezone = pytz.timezone('Europe/Istanbul')
            su_an = datetime.now(tr_timezone).strftime("%d %B %Y %A, %H:%M")

            # Tamamen standart Gemini 3 Flash
            model_name = "gemini-3-flash-preview"
            url = f"https://generativelanguage.googleapis.com/v1beta/models/{model_name}:generateContent?key={api_key}"
            
            # Kısıtlamaları kaldırdık, saf zekayı bıraktık
            sistem_talimati = (
                f"Sistem tarihi ve saati: {su_an}. Bu bilgiyi kendi iç muhakemende kullan ama "
                "kullanıcı özellikle sormadıkça cevaplarında 'Bugün günlerden...' gibi ifadeler kullanma. "
                "Sen çok yönlü, yetenekli ve doğal bir yapay zeka asistanısın. Hiçbir mesleki persona (katip vs.) üstlenme. "
                "Kullanıcıya yazılım, günlük hayat, bilim dahil her konuda yardımcı ol. "
                "Yanıtlarını her zaman düzgün paragraflar, maddeler ve Markdown formatı kullanarak ver."
            )

            payload = {
                "contents": [{"parts": [{"text": f"{sistem_talimati}\n\nKullanıcı: {user_text}"}]}]
            }
            
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            
            # Vercel sınırına takılmaması için hızlı yanıt verecek
            with urllib.request.urlopen(req, timeout=9) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
            
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))

        except Exception as e:
            self.send_response(200) # Hatayı JS tarafında yakalamak için 200 dönüyoruz
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": f"Bağlantı zaman aşımına uğradı. Lütfen tekrar gönderin."}).encode('utf-8'))
