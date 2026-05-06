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

            # Senin listendeki en yeni ve stabil Gemini 3.1 sürümü
            model_name = "gemini-3.1-flash-lite-preview" 
            url = f"https://generativelanguage.googleapis.com/v1beta/models/{model_name}:generateContent?key={api_key}"
            
            # Halüsinasyon önleyici, derin düşünme ve internet arama talimatı
            sistem_talimati = (
                "Sen Gemini 3.1 mimarisi üzerine kurulu, derin muhakeme yeteneği olan "
                "kıdemli bir hukuk asistanısın. Yargıtay kararları veya mevzuat sorulduğunda "
                "uydurma bilgi vermemek için mutlaka Google Arama motorunu kullan. "
                "Cevaplarını şu formatta sun:\n"
                "[DÜŞÜNCE]\n(Arama ve analiz süreci)\n"
                "[CEVAP]\n(Gerçek verilere dayalı yanıt)"
            )
            
            payload = {
                "contents": [{"parts": [{"text": f"{sistem_talimati}\n\nKullanıcı: {user_text}"}]}],
                "tools": [{"google_search_retrieval": {}}] 
            }
            
            req = urllib.request.Request(
                url, 
                data=json.dumps(payload).encode('utf-8'), 
                headers={'Content-Type': 'application/json'}
            )

            with urllib.request.urlopen(req, timeout=45) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                
                # Gemini 3 serisinde yanıt yapısı bazen farklılık gösterebilir, garantili çekiyoruz
                if 'candidates' in gemini_json and len(gemini_json['candidates']) > 0:
                    candidate = gemini_json['candidates'][0]
                    if 'content' in candidate and 'parts' in candidate['content']:
                        ai_cevabi = candidate['content']['parts'][0]['text']
                    else:
                        ai_cevabi = "[CEVAP] Model cevap üretti ancak beklenen formatta değil."
                else:
                    ai_cevabi = "[CEVAP] Google şu an bu sorguya yanıt veremedi."
            
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))

        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": f"Gemini 3 Bağlantı Hatası: {str(e)}"}).encode('utf-8'))
