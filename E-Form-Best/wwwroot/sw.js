self.addEventListener('push', function (event) {
    if (event.data) {
        const data = event.data.json();
        const options = {
            body: data.message,
            icon: '/favicon.ico', // Thay bằng logo của bạn
            badge: '/favicon.ico',
            vibrate: [100, 50, 100],
            data: { url: data.url },
            tag: 'it-notification', // Giúp gộp các thông báo lại
            renotify: true
        };

        event.waitUntil(
            self.registration.showNotification(data.title, options)
        );
    }
});

// Khi người dùng click vào cái bảng thông báo trên Windows/Mac
self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    event.waitUntil(
        clients.openWindow(event.notification.data.url)
    );
});