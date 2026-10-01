# Установка Native Video

Пакет предназначен для Godot 4.6+ на Windows x86_64 и GNU/Linux x86_64.

1. Распакуйте архив в корень проекта Godot. В результате файл расширения
   должен находиться по пути `res://addons/native_video/native_video.gdextension`.
2. Полностью перезапустите редактор Godot.
3. Используйте `NativeVideoStream` через обычный `VideoStreamPlayer`.

В Windows дополнительных мультимедийных пакетов не требуется: используется
Media Foundation. Сборка проверена в Godot 4.7.1 Mono с рендерером Vulkan.

В Linux используются динамические системные библиотеки GStreamer 1.x. Для
Debian/Ubuntu установите runtime-компоненты:

```bash
sudo apt install libgstreamer1.0-0 libgstreamer-plugins-base1.0-0 \
  gstreamer1.0-plugins-base gstreamer1.0-plugins-good \
  gstreamer1.0-plugins-bad gstreamer1.0-libav gstreamer1.0-vaapi
```

Linux-сборка рассчитана на glibc 2.36+ и Vulkan (Forward+ или Mobile).
Аппаратный декодер обязателен по умолчанию. Если он недоступен, расширение
выводит понятную ошибку. Явно разрешить программный fallback для диагностики
можно переменной окружения `GODOT_NATIVE_VIDEO_ALLOW_SOFTWARE=1`.

Подробности о кодеках, драйверах и диагностике находятся в `README.md`.
