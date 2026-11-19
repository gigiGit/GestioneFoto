#include <napi.h>

Napi::String Saluta(const Napi::CallbackInfo& info) {
    Napi::Env env = info.Env();
    return Napi::String::New(env, "Ciao da C++!");
}

Napi::Object Init(Napi::Env env, Napi::Object exports) {
    exports.Set("saluta", Napi::Function::New(env, Saluta));
    return exports;
}

NODE_API_MODULE(mio_modulo, Init)
