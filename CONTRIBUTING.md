# 🤝 Katkıda Bulunma Rehberi

CleanTenant'a katkıda bulunmak istediğiniz için teşekkür ederiz!

## Geliştirme Süreci

1. Repository'yi fork edin
2. Feature branch oluşturun: `git checkout -b feature/amazing-feature`
3. Değişikliklerinizi commit edin: `git commit -m 'feat: add amazing feature'`
4. Branch'i push edin: `git push origin feature/amazing-feature`
5. Pull Request açın

## Commit Mesajı Kuralları

[Conventional Commits](https://www.conventionalcommits.org/) standardını kullanıyoruz:

- `feat:` Yeni özellik
- `fix:` Hata düzeltme
- `docs:` Dokümantasyon değişikliği
- `refactor:` Kod yeniden yapılandırma
- `test:` Test ekleme veya düzeltme
- `chore:` Build, CI/CD değişiklikleri

## Kod Standartları

- Tüm public method'larda XML documentation zorunludur
- Türkçe ve İngilizce açıklama yorumları eklenir
- `TreatWarningsAsErrors` aktiftir — uyarısız kod zorunludur
- Her yeni özellik için birim test yazılmalıdır

## Güvenlik

Güvenlik açığı bulduysanız lütfen public issue **açmayın**. Bunun yerine doğrudan iletişime geçin.
