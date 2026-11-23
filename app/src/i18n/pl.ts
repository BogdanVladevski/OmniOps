import type { Strings } from './en';

// Aplikacja dla kierowców — polski.
export const pl: Strings = {
  language: 'Język',
  signOut: 'Wyloguj',
  driver: 'Kierowca',
  pleaseWait: 'Proszę czekać…',

  yourJobs: 'Twoje zlecenia',
  noJobsTitle: 'Nie masz teraz zleceń',
  noJobsBody: 'Gdy biuro przydzieli ci dostawę, pojawi się tutaj.',
  offlineJobs: 'Brak połączenia. Pokazuję zlecenia zapisane na tym telefonie.',
  order: 'Zamówienie {ref}',
  allJobs: '‹ Wszystkie zlecenia',
  keepAt: 'Przechowywać w {range}',

  'status.Planned': 'Zaplanowane',
  'status.InProgress': 'W drodze',
  'status.Delivered': 'Dostarczone',
  'status.Cancelled': 'Anulowane',

  waiting: 'Zapisane na tym telefonie: {count}. Zostanie wysłane, gdy będzie zasięg.',
  sending: 'Wysyłanie…',
  refused: 'Nie przyjęto: {message}',
  remove: 'Usuń',

  takePhoto: 'Zrób zdjęcie',
  fromGallery: 'Z galerii',
  cameraOff: 'Aparat jest wyłączony. Włącz go w Ustawieniach albo wybierz zdjęcie z galerii.',

  'quality.blurry': 'To zdjęcie wygląda na nieostre. Biuro i klient mogą go nie odczytać.',
  'quality.dark': 'To zdjęcie jest bardzo ciemne. Biuro i klient mogą go nie odczytać.',
  retake: 'Zrób ponownie',
  keepPhoto: 'Użyj mimo to',

  'loading.title': 'Dokument załadunku',
  'loading.hint': 'Sfotografuj podpisany dokument załadunku lub CMR przy odbiorze towaru. Trafia razem z fakturą.',
  'loading.count': 'Dołączone strony: {count}',
  'loading.add': 'Sfotografuj dokument załadunku',
  'loading.addAnother': 'Dodaj kolejną stronę',

  'temp.title': 'Temperatura ładunku',
  'temp.hint': 'Zapisuj temperaturę przy załadunku, co 2 godziny w trasie i przy dostawie.',
  'temp.last': 'Ostatnio: {value} o {time}.',
  'temp.dueNow': 'Następna kontrola jest teraz.',
  'temp.dueBy': 'Następna kontrola do {time}.',
  'temp.field': 'Odczyt z wyświetlacza agregatu (°C)',
  'temp.placeholder': 'np. 4,5',
  'temp.invalid': 'Wpisz liczbę, np. 4,5 lub −18.',
  'temp.enterFirst': 'Najpierw wpisz temperaturę z wyświetlacza.',
  'temp.save': 'Sfotografuj wyświetlacz i zapisz',
  'temp.saved': 'Zapisano {value}.',
  'temp.outOfRange':
    '{value} jest poza zakresem {range}. Sprawdź ustawienie agregatu. Biuro zostanie powiadomione, gdy tylko to zostanie wysłane.',

  'problem.title': 'Zgłoś problem',
  'problem.hint':
    'Uszkodzenie, brakujący towar lub inny problem, przy załadunku albo dostawie. Zdjęcie to dokumentuje.',
  'problem.damaged': 'Uszkodzone',
  'problem.short': 'Brak',
  'problem.wrong': 'Zły towar',
  'problem.refused': 'Odmowa przyjęcia',
  'problem.other': 'Inne',
  'problem.describe': 'Co jest nie tak?',
  'problem.placeholder': 'np. 2 kartony zgniecione',
  'problem.send': 'Sfotografuj i zgłoś',
  'problem.done': 'Problem zapisany. Biuro zostanie powiadomione, gdy tylko to zostanie wysłane.',
  'problem.count': 'Zgłoszone problemy: {count}',

  'start.hint': 'Gdy ruszasz, rozpocznij kurs. Twoja lokalizacja jest udostępniana biuru i klientowi do czasu dostawy.',
  'start.button': 'Rozpocznij kurs',

  'gps.sharing': 'Lokalizacja jest udostępniana. Nie zamykaj aplikacji w czasie jazdy.',
  'gps.sentAt': 'Lokalizacja jest udostępniana, ostatnio wysłano o {time}. Nie zamykaj aplikacji w czasie jazdy.',
  'gps.offline': 'Brak połączenia. Zapisane lokalizacje: {count}. Zostaną wysłane po odzyskaniu połączenia.',
  'gps.denied':
    'Lokalizacja jest wyłączona dla OmniOps. Włącz ją w Ustawieniach, aby biuro i klient mogli śledzić dostawę.',
  'gps.starting': 'Włączanie udostępniania lokalizacji…',
  'gps.error': 'Problem z lokalizacją: {message}',

  'pod.title': 'Potwierdzenie dostawy',
  'pod.hint':
    'Sfotografuj podpisany dokument dostawy lub towar przy drzwiach albo poproś odbiorcę o podpis na ekranie.',
  'pod.hintCold': 'Potrzebny jest też odczyt temperatury przy drzwiach.',
  'pod.photos': 'Dołączone zdjęcia: {count}',
  'pod.receivedBy': 'Odebrał(a)',
  'pod.receivedByPlaceholder': 'Imię i nazwisko osoby, która odebrała towar',
  'pod.note': 'Uwaga (opcjonalnie)',
  'pod.notePlaceholder': 'np. Zostawione przy bramie 2',
  'pod.needReading': 'Przed potwierdzeniem zapisz temperaturę przy drzwiach.',
  'pod.needProof': 'Dodaj zdjęcie lub podpis odbiorcy.',
  'pod.needName': 'Wpisz imię i nazwisko odbiorcy.',
  'pod.confirm': 'Potwierdź dostawę',

  'sign.open': 'Odbiorca podpisuje na ekranie',
  'sign.title': 'Podpis odbiorcy',
  'sign.hint': 'Poproś osobę {name} o podpis palcem w ramce.',
  'sign.needName': 'Najpierw wpisz imię i nazwisko odbiorcy.',
  'sign.clear': 'Wyczyść',
  'sign.save': 'Zapisz podpis',
  'sign.done': 'Podpisał(a): {name}',

  'done.title': 'Dostarczone',
  'done.by': 'Odebrał(a): {name}',
  'done.pending': 'Zapisane na tym telefonie. Zostanie wysłane, gdy będzie zasięg.',

  'wait.arrivePickup': 'Jestem na załadunku',
  'wait.arriveDelivery': 'Jestem u klienta',
  'wait.hint': 'Dotknij, gdy dojedziesz do bramy. Jeśli musisz czekać, czas zostanie zapisany do faktury.',
  'wait.since': 'Przyjazd o {time}. Oczekiwanie: {duration}.',
  'unit.h': 'godz.',
  'unit.min': 'min',
  connectTitle: 'Połącz ten telefon',
  connectIntro: 'Wpisz 8-znakowy kod z biura. Robisz to tylko raz.',
  connectCode: 'Kod',
  connect: 'Połącz',
};
