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

            # "Düşünen" zeka için 3.1 Pro veya Flash modelini kullanıyoruz
            model_name = "gemini-3.1-flash-lite-preview" 
            url = f"https://generativelanguage.googleapis.com/v1beta/models/{model_name}:generateContent?key={api_key}"
            
            # MUHAKEME TALİMATI: Yapay zekayı 'düşünmeye' zorluyoruz
            sistem_talimati = (
                "Sen derin düşünme yeteneğine sahip bir asistansın. Cevap vermeden önce "
                "metni hukuki, teknik ve mantıksal açılardan analiz et. Yanıtını şu formatta ver:\n"
                "[DÜŞÜNCE]\n(Buraya yaptığın analizi ve izlediğin adımları yaz)\n"
                "[CEVAP]\n(Buraya kullanıcıya vereceğin asıl yanıtı yaz)"
            )
            
            payload = {
                "contents": [{"parts": [{"text": f"{sistem_talimati}\n\nKullanıcı: {user_text}"}]}]
            }
            
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
