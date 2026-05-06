from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            user_text = body.get('text', '')

            api_key = os.environ.get('GEMINI_API_KEY', '').strip()
            
            # 2026 güncel en stabil URL formatı
            url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={api_key}"
            
            payload = {
                "contents": [{"parts": [{"text": user_text}]}]
            }
            
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            
            with urllib.request.urlopen(req) as response:
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
            # Hatanın tam URL'ini de görelim ki neresi yanlış anlıyalım
            self.wfile.write(json.dumps({"error": f"Hata: {str(e)}"}).encode('utf-8'))
