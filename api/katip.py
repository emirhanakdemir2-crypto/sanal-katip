from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request
import urllib.error

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            user_text = body.get('text', '')
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()

            # Kotası daha yüksek ve çok daha hızlı olan Flash modelini seçiyoruz
            model_name = "gemini-3-flash-preview" 
            url = f"https://generativelanguage.googleapis.com/v1beta/models/{model_name}:generateContent?key={api_key}"
            
            sistem_talimati = (
                "Sen profesyonel bir adliye katibi asistanısın. Görevin; verilen metinlerden "
                "TC kimlik no, isim, dosya numarası ayıklamak, hukuki metinleri özetlemek "
                "veya dilekçeleri düzenlemektir. Yanıtlarını net ve resmi bir dille ver."
            )
            
            payload = {
                "contents": [{
                    "parts": [{"text": f"{sistem_talimati}\n\nİşlenecek Metin:\n{user_text}"}]
                }]
            }
            
            req = urllib.request.Request(
                url, 
                data=json.dumps(payload).encode('utf-8'), 
                headers={'Content-Type': 'application/json'}
            )

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
            self.wfile.write(json.dumps({"error": f"Asistan meşgul: {str(e)}"}).encode('utf-8'))
