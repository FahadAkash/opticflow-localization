import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Award,
  BedDouble,
  CheckCircle2,
  Clock,
  Compass,
  Eye,
  Globe,
  Info,
  Layers,
  MapPin,
  Phone,
  ShieldCheck,
  Sparkles,
  Utensils,
  Wifi,
  Zap,
} from 'lucide-react';
import { optiFlowApi } from '../api/client';
import type { ContentPreviewBlock, ContentPreviewResponse } from '../api/types';
import { formatLocaleLabel } from '../utils/diff';

// --- Global Chrome Localization Dictionaries ---
// In enterprise CMS architectures (e.g. Optimizely CMS), global chrome (header, top announcement bar,
// navigation menu, common amenities, footer) is managed either via a shared Global CMS Container
// or front-end UI localization dictionaries. Here, we provide complete translations for all supported
// guest languages (English, Spanish, German, French, and Bengali).

interface ChromeTranslations {
  address: string;
  stars: string;
  brandTagline: string;
  nav: {
    home: string;
    rooms: string;
    dining: string;
    spa: string;
    offers: string;
    bookDirect: string;
  };
  amenitiesSectionTitle: string;
  amenities: {
    wifi: { title: string; desc: string };
    gastronomy: { title: string; desc: string };
    yacht: { title: string; desc: string };
    butler: { title: string; desc: string };
  };
  footer: {
    copyright: string;
    contentKeyLabel: string;
    targetLocaleLabel: string;
  };
}

const CHROME_DICTIONARY: Record<string, ChromeTranslations> = {
  en: {
    address: 'Riviera Coastal Promenade 104, Nice / Monaco',
    stars: '★★★★★ 5-Star Luxury Resort',
    brandTagline: 'Resort & Suites',
    nav: {
      home: 'Home',
      rooms: 'Rooms & Suites',
      dining: 'Fine Dining',
      spa: 'Wellness Spa',
      offers: 'Special Offers',
      bookDirect: 'Book Direct',
    },
    amenitiesSectionTitle: 'Included World-Class Amenities',
    amenities: {
      wifi: {
        title: 'Ultra-Fast Fiber WiFi',
        desc: 'Seamless high-speed streaming across all suites and beaches.',
      },
      gastronomy: {
        title: 'Michelin Gastronomy',
        desc: 'Hand-crafted Mediterranean tasting menus by Chef Laurent.',
      },
      yacht: {
        title: 'Private Yacht Excursions',
        desc: 'Daily chartered cruises across hidden coves and sapphire bays.',
      },
      butler: {
        title: '24/7 Dedicated Butler',
        desc: 'Personalized itinerary planning, unpacking, and cocktail service.',
      },
    },
    footer: {
      copyright: 'Grand Azure Luxury Hotel & Suites. Powered by Optimizely CMS & OptiFlow Incremental Localization Engine.',
      contentKeyLabel: 'Content Key',
      targetLocaleLabel: 'Target Locale',
    },
  },
  'es-es': {
    address: 'Paseo Costero de la Riviera 104, Niza / Mónaco',
    stars: '★★★★★ Resort de Lujo 5 Estrellas',
    brandTagline: 'Resort y Suites',
    nav: {
      home: 'Inicio',
      rooms: 'Habitaciones y Suites',
      dining: 'Alta Gastronomía',
      spa: 'Spa y Bienestar',
      offers: 'Ofertas Especiales',
      bookDirect: 'Reservar Directo',
    },
    amenitiesSectionTitle: 'Servicios de Clase Mundial Incluidos',
    amenities: {
      wifi: {
        title: 'WiFi de Fibra Ultrarrápido',
        desc: 'Transmisión ininterrumpida de alta velocidad en suites y playas.',
      },
      gastronomy: {
        title: 'Gastronomía Michelin',
        desc: 'Menús degustación mediterráneos elaborados por el Chef Laurent.',
      },
      yacht: {
        title: 'Excursiones Privadas en Yate',
        desc: 'Cruceros chárter diarios por calas escondidas y bahías de zafiro.',
      },
      butler: {
        title: 'Mayordomo Dedicado 24/7',
        desc: 'Planificación personalizada de itinerarios, desempacado y coctelería.',
      },
    },
    footer: {
      copyright: 'Grand Azure Hotel y Suites de Lujo. Desarrollado con Optimizely CMS y el motor de localización incremental OptiFlow.',
      contentKeyLabel: 'Clave de Contenido',
      targetLocaleLabel: 'Idioma Destino',
    },
  },
  'de-de': {
    address: 'Riviera Küstenpromenade 104, Nizza / Monaco',
    stars: '★★★★★ 5-Sterne-Luxusresort',
    brandTagline: 'Resort & Suiten',
    nav: {
      home: 'Startseite',
      rooms: 'Zimmer & Suiten',
      dining: 'Gourmet-Gastronomie',
      spa: 'Wellness & Spa',
      offers: 'Sonderangebote',
      bookDirect: 'Direkt buchen',
    },
    amenitiesSectionTitle: 'Inklusive Weltklasse-Annehmlichkeiten',
    amenities: {
      wifi: {
        title: 'Ultraschnelles Glasfaser-WLAN',
        desc: 'Nahtloses Highspeed-Streaming in allen Suiten und am Privatstrand.',
      },
      gastronomy: {
        title: 'Michelin-Gastronomie',
        desc: 'Handgefertigte mediterrane Degustationsmenüs von Chefkoch Laurent.',
      },
      yacht: {
        title: 'Private Yacht-Ausflüge',
        desc: 'Tägliche Kreuzfahrten zu versteckten Buchten und saphirblauem Wasser.',
      },
      butler: {
        title: 'Engagierter 24/7 Butler-Service',
        desc: 'Individuelle Reiseplanung, Auspackservice und Cocktails rund um die Uhr.',
      },
    },
    footer: {
      copyright: 'Grand Azure Luxushotel & Suiten. Bereitgestellt über Optimizely CMS & OptiFlow Lokalisierungs-Engine.',
      contentKeyLabel: 'Inhaltsschlüssel',
      targetLocaleLabel: 'Zielsprache',
    },
  },
  'fr-fr': {
    address: 'Promenade Côtière de la Riviera 104, Nice / Monaco',
    stars: '★★★★★ Complexe Hôtelier 5 Étoiles de Luxe',
    brandTagline: 'Hôtel & Suites',
    nav: {
      home: 'Accueil',
      rooms: 'Chambres & Suites',
      dining: 'Gastronomie Étoilée',
      spa: 'Spa & Bien-être',
      offers: 'Offres Spéciales',
      bookDirect: 'Réserver Directement',
    },
    amenitiesSectionTitle: "Prestations d'Excellence Incluses",
    amenities: {
      wifi: {
        title: 'Fibre WiFi Ultra-Rapide',
        desc: 'Connexion haut débit fluide dans toutes les suites et sur la plage.',
      },
      gastronomy: {
        title: 'Gastronomie Étoilée Michelin',
        desc: 'Menus dégustation méditerranéens préparés par le Chef Laurent.',
      },
      yacht: {
        title: 'Excursions en Yacht Privé',
        desc: 'Croisières quotidiennes vers des criques secrètes et baies azurées.',
      },
      butler: {
        title: 'Majordome Dédié 24h/24',
        desc: 'Itinéraire sur mesure, service de bagages et dégustation de cocktails.',
      },
    },
    footer: {
      copyright: 'Grand Azure Hôtel & Suites de Luxe. Propulsé par Optimizely CMS & Moteur de Localisation Incrémentale OptiFlow.',
      contentKeyLabel: 'Clé de Contenu',
      targetLocaleLabel: 'Langue Cible',
    },
  },
  bd: {
    address: 'রিভিয়েরা কোস্টাল প্রমনেড ১০৪, নিস / মোনাকো',
    stars: '★★★★★ ৫-স্টার বিলাসবহুল রিসোর্ট',
    brandTagline: 'রিসোর্ট এবং স্যুট',
    nav: {
      home: 'হোম',
      rooms: 'রুম ও স্যুট',
      dining: 'ফাইন ডাইনিং',
      spa: 'ওয়েলনেস স্পা',
      offers: 'বিশেষ অফার',
      bookDirect: 'সরাসরি বুক করুন',
    },
    amenitiesSectionTitle: 'বিশ্বমানের সুযোগ-সুবিধা অন্তর্ভুক্ত',
    amenities: {
      wifi: {
        title: 'আল্ট্রা-ফাস্ট ফাইবার ওয়াইফাই',
        desc: 'সব স্যুট এবং সৈকতে নির্বিঘ্ন দ্রুতগতির ইন্টারনেট স্ট্রিমিং।',
      },
      gastronomy: {
        title: 'মিশেলিন তারকাখচিত গ্যাস্ট্রোনমি',
        desc: 'শেফ লরেন্টের হাতে তৈরি ভূমধ্যসাগরীয় বিশেষ লোভনীয় মেনু।',
      },
      yacht: {
        title: 'ব্যক্তিগত ইয়ট ভ্রমণ',
        desc: 'লুকানো উপসাগর ও নীল জলরাশিতে দৈনিক চার্টার্ড বিলাসবহুল ক্রুজ।',
      },
      butler: {
        title: '২৪/৭ নিবেদিত বাটলার সেবা',
        desc: 'ব্যক্তিগত ভ্রমণ পরিকল্পনা, লাগেজ গোছানো এবং ককটেল পরিষেবা।',
      },
    },
    footer: {
      copyright: 'গ্র্যান্ড অ্যাজুর বিলাসবহুল হোটেল ও স্যুট। অপটিমাইযলি সিএমএস এবং অপটিফ্লো ইঞ্জিন দ্বারা পরিচালিত।',
      contentKeyLabel: 'কন্টেন্ট কি',
      targetLocaleLabel: 'টার্গেট ভাষা',
    },
  },
};

function getChrome(locale: string): ChromeTranslations {
  const norm = locale.trim().toLowerCase();
  const en = CHROME_DICTIONARY['en']!;
  if (CHROME_DICTIONARY[norm]) return CHROME_DICTIONARY[norm]!;
  if (norm.startsWith('es')) return CHROME_DICTIONARY['es-es'] ?? en;
  if (norm.startsWith('de')) return CHROME_DICTIONARY['de-de'] ?? en;
  if (norm.startsWith('fr')) return CHROME_DICTIONARY['fr-fr'] ?? en;
  if (norm.startsWith('bd') || norm.startsWith('bn')) return CHROME_DICTIONARY['bd'] ?? en;
  return en;
}

function renderSafeContent(html: string) {
  if (!html) return null;
  const clean = html.replace(/<p>/gi, '').replace(/<\/p>/gi, '');
  const parts = clean.split(/(<strong>.*?<\/strong>|<b>.*?<\/b>|<em>.*?<\/em>|<i>.*?<\/i>)/gi);

  return (
    <span>
      {parts.map((part, idx) => {
        if (/^<(strong|b)>(.*?)<\/(strong|b)>$/i.test(part)) {
          const inner = part.replace(/^<(strong|b)>/i, '').replace(/<\/(strong|b)>$/i, '');
          return <strong key={idx}>{inner}</strong>;
        }
        if (/^<(em|i)>(.*?)<\/(em|i)>$/i.test(part)) {
          const inner = part.replace(/^<(em|i)>/i, '').replace(/<\/(em|i)>$/i, '');
          return <em key={idx}>{inner}</em>;
        }
        return part;
      })}
    </span>
  );
}

function provenanceBadge(provenance: string) {
  switch (provenance) {
    case 'HumanApproved':
      return (
        <span
          className="badge"
          style={{
            backgroundColor: '#dcfce7',
            color: '#15803d',
            border: '1px solid #86efac',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
          }}
        >
          <CheckCircle2 size={12} /> Human Approved
        </span>
      );
    case 'AiGenerated':
      return (
        <span
          className="badge"
          style={{
            backgroundColor: '#fef3c7',
            color: '#b45309',
            border: '1px solid #fde68a',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
          }}
        >
          <Zap size={12} /> AI Generated
        </span>
      );
    case 'SourceMaster':
      return (
        <span
          className="badge"
          style={{
            backgroundColor: '#f1f5f9',
            color: '#475569',
            border: '1px solid #cbd5e1',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
          }}
        >
          <ShieldCheck size={12} /> Source Master (EN)
        </span>
      );
    default:
      return (
        <span
          className="badge"
          style={{
            backgroundColor: '#fef2f2',
            color: '#b91c1c',
            border: '1px solid #fecaca',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
          }}
        >
          <Clock size={12} /> Untranslated (Source Fallback)
        </span>
      );
  }
}

export function HotelPreviewView({ isAuthenticated }: { readonly isAuthenticated: boolean }) {
  const queryClient = useQueryClient();
  const [selectedItemId, setSelectedItemId] = useState<string>('');
  const [selectedLocale, setSelectedLocale] = useState<string>('BD');
  const [inspectorMode, setInspectorMode] = useState<boolean>(true);
  const [reservationNotice, setReservationNotice] = useState<string | null>(null);

  const itemsQuery = useQuery({
    queryKey: ['content-items'],
    queryFn: optiFlowApi.listContentItems,
    enabled: isAuthenticated,
  });

  const effectiveItemId = selectedItemId || itemsQuery.data?.[0]?.id || '';
  const currentItem = itemsQuery.data?.find((i) => i.id === effectiveItemId);

  const previewQuery = useQuery<ContentPreviewResponse>({
    queryKey: ['content-preview', effectiveItemId, selectedLocale],
    queryFn: () => optiFlowApi.getContentPreview(effectiveItemId, selectedLocale),
    enabled: isAuthenticated && effectiveItemId.length > 0,
  });

  const simulateMutation = useMutation({
    mutationFn: (scenario: 'v2' | 'rollback') => optiFlowApi.simulateCmsUpdate(scenario),
    onSuccess: (res) => {
      setReservationNotice(res.message);
      void queryClient.invalidateQueries({ queryKey: ['content-items'] });
      void queryClient.invalidateQueries({ queryKey: ['content-preview'] });
    },
  });

  const blocks: readonly ContentPreviewBlock[] = previewQuery.data?.blocks ?? [];
  const findBlock = (key: string) => blocks.find((b) => b.blockKey === key);

  // Available locales merge from preview API response + standard list so user can freely preview any supported language
  const standardLocales = ['en', 'BD', 'es-ES', 'fr-FR', 'de-DE'];
  const serverLocales = previewQuery.data?.availableLocales ?? [];
  const availableLocales = Array.from(new Set([...serverLocales, ...standardLocales]));

  const chrome = getChrome(selectedLocale);

  // Helper to switch page via navigation links
  const navigateToPage = (externalKeyPattern: string) => {
    const targetItem = itemsQuery.data?.find(
      (item) =>
        item.externalContentKey.toLowerCase().includes(externalKeyPattern.toLowerCase()) ||
        item.contentType.toLowerCase().includes(externalKeyPattern.toLowerCase())
    );
    if (targetItem) {
      setSelectedItemId(targetItem.id);
    }
  };

  const isCurrentPage = (externalKeyPattern: string) => {
    if (!currentItem) return false;
    return (
      currentItem.externalContentKey.toLowerCase().includes(externalKeyPattern.toLowerCase()) ||
      currentItem.contentType.toLowerCase().includes(externalKeyPattern.toLowerCase())
    );
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Top Controls Bar */}
      <div className="card" style={{ padding: 18 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
          <div>
            <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
              <Eye size={20} color="#0284c7" />
              <span>Live Hotel Website Preview</span>
              <span className="badge badge-blue">Multi-Page & Localized Chrome</span>
            </h3>
            <p style={{ margin: '4px 0 0 0', color: '#64748b', fontSize: '0.85rem' }}>
              Preview how localized page blocks and global chrome (header, navigation, amenities, footer) render for international guests.
            </p>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <label style={{ fontSize: '0.85rem', color: '#475569', fontWeight: 600 }}>Active Page:</label>
            <select
              value={effectiveItemId}
              onChange={(e) => setSelectedItemId(e.target.value)}
              disabled={!itemsQuery.data?.length}
              style={{ fontWeight: 600, minWidth: 240 }}
            >
              {itemsQuery.data?.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.externalContentKey === 'hotel-home-001'
                    ? '🏠 Home (hotel-home-001)'
                    : item.externalContentKey === 'hotel-rooms-002'
                    ? '🏨 Rooms & Suites (hotel-rooms-002)'
                    : item.externalContentKey === 'hotel-dining-003'
                    ? '🍽️ Fine Dining & Spa (hotel-dining-003)'
                    : `${item.externalContentKey} (${item.contentType})`}
                </option>
              ))}
            </select>

            <button
              type="button"
              className={`btn btn-sm ${inspectorMode ? 'btn-primary' : 'btn-outline'}`}
              onClick={() => setInspectorMode(!inspectorMode)}
              style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
            >
              <Layers size={14} />
              {inspectorMode ? 'Inspector Mode: ON' : 'Inspector Mode: OFF'}
            </button>
          </div>
        </div>

        {/* Locale Selector Bar */}
        <div
          style={{
            marginTop: 16,
            paddingTop: 14,
            borderTop: '1px solid #e2e8f0',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 10,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ fontSize: '0.82rem', fontWeight: 600, color: '#334155', display: 'flex', alignItems: 'center', gap: 4 }}>
              <Globe size={15} /> Select Guest Language:
            </span>
            {availableLocales.map((loc) => {
              const label = formatLocaleLabel(loc);
              const isSelected = selectedLocale.toLowerCase() === loc.toLowerCase();
              return (
                <button
                  key={loc}
                  type="button"
                  onClick={() => setSelectedLocale(loc)}
                  className={`btn btn-sm ${isSelected ? 'btn-primary' : 'btn-outline'}`}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    padding: '6px 12px',
                    fontWeight: isSelected ? 600 : 400,
                  }}
                >
                  <span>{label.flag}</span>
                  <span>{label.name}</span>
                </button>
              );
            })}
          </div>

          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              style={{ fontSize: '0.78rem' }}
              disabled={simulateMutation.isPending}
              onClick={() => simulateMutation.mutate('v2')}
              title="Simulates CMS update: modifies hero title and adds amenities banner"
            >
              Simulate CMS v2 Update
            </button>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              style={{ fontSize: '0.78rem' }}
              disabled={simulateMutation.isPending}
              onClick={() => simulateMutation.mutate('rollback')}
              title="Simulates CMS rollback to original v1 content as v3"
            >
              Rollback to v1 (as v3)
            </button>
          </div>
        </div>

        {/* Architecture Info Pill */}
        {inspectorMode && (
          <div
            style={{
              marginTop: 14,
              padding: '8px 14px',
              backgroundColor: '#f0fdf4',
              borderRadius: 8,
              border: '1px solid #bbf7d0',
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              fontSize: '0.8rem',
              color: '#166534',
            }}
          >
            <Info size={16} color="#16a34a" style={{ flexShrink: 0 }} />
            <span>
              <strong>Architectural Cohesion:</strong> Global Chrome (Header, Navigation, Amenities & Footer) is localized
              synchronously via the CMS Global Container / i18n dictionary for <strong>{formatLocaleLabel(selectedLocale).name}</strong>.
              Body blocks (Titles, Descriptions, Rich Text) flow from OptiFlow&apos;s AI localization engine and human review pipeline.
            </span>
          </div>
        )}

        {reservationNotice && (
          <div className="alert alert-info" style={{ marginTop: 12, display: 'flex', justifyContent: 'space-between' }}>
            <span>{reservationNotice}</span>
            <button
              type="button"
              onClick={() => setReservationNotice(null)}
              style={{ background: 'none', border: 'none', cursor: 'pointer', fontWeight: 'bold' }}
            >
              ×
            </button>
          </div>
        )}
      </div>

      {/* Rendered Hotel Website */}
      <div
        style={{
          borderRadius: 16,
          overflow: 'hidden',
          boxShadow: '0 20px 40px -15px rgba(0,0,0,0.2)',
          border: '1px solid #cbd5e1',
          backgroundColor: '#ffffff',
          fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
        }}
      >
        {/* Hotel Top Announcement Bar (DYNAMICALLY LOCALIZED) */}
        <div
          style={{
            backgroundColor: '#0f172a',
            color: '#cbd5e1',
            padding: '8px 24px',
            fontSize: '0.8rem',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 8,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <MapPin size={13} color="#38bdf8" /> {chrome.address}
            </span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <Phone size={13} color="#38bdf8" /> +33 (0) 4 93 00 22 11
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <span style={{ color: '#fbbf24', display: 'inline-flex', alignItems: 'center', gap: 4, fontWeight: 600 }}>
              <Award size={14} /> {chrome.stars}
            </span>
            <span style={{ color: '#94a3b8' }}>|</span>
            <span style={{ fontWeight: 600, color: '#f8fafc' }}>
              {formatLocaleLabel(selectedLocale).flag} {selectedLocale.toUpperCase()}
            </span>
          </div>
        </div>

        {/* Hotel Navigation Bar (DYNAMICALLY LOCALIZED & INTERACTIVE) */}
        <header
          style={{
            padding: '18px 32px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            borderBottom: '1px solid #f1f5f9',
            backgroundColor: '#ffffff',
            flexWrap: 'wrap',
            gap: 12,
          }}
        >
          <div
            style={{ display: 'flex', alignItems: 'center', gap: 12, cursor: 'pointer' }}
            onClick={() => navigateToPage('home')}
          >
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: 8,
                backgroundColor: '#1e293b',
                color: '#f59e0b',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontWeight: 700,
                fontSize: '1.2rem',
                letterSpacing: 1,
              }}
            >
              GA
            </div>
            <div>
              <div style={{ fontWeight: 800, fontSize: '1.15rem', letterSpacing: 0.5, color: '#0f172a' }}>
                GRAND AZURE
              </div>
              <div style={{ fontSize: '0.68rem', letterSpacing: 2, color: '#94a3b8', textTransform: 'uppercase' }}>
                {chrome.brandTagline}
              </div>
            </div>
          </div>

          <nav style={{ display: 'flex', gap: 20, fontSize: '0.88rem', fontWeight: 500, color: '#475569', flexWrap: 'wrap' }}>
            <span
              style={{
                color: isCurrentPage('home') ? '#0284c7' : '#475569',
                cursor: 'pointer',
                fontWeight: isCurrentPage('home') ? 700 : 500,
                borderBottom: isCurrentPage('home') ? '2px solid #0284c7' : 'none',
                paddingBottom: 2,
              }}
              onClick={() => navigateToPage('home')}
            >
              {chrome.nav.home}
            </span>
            <span
              style={{
                color: isCurrentPage('rooms') ? '#0284c7' : '#475569',
                cursor: 'pointer',
                fontWeight: isCurrentPage('rooms') ? 700 : 500,
                borderBottom: isCurrentPage('rooms') ? '2px solid #0284c7' : 'none',
                paddingBottom: 2,
              }}
              onClick={() => navigateToPage('rooms')}
            >
              {chrome.nav.rooms}
            </span>
            <span
              style={{
                color: isCurrentPage('dining') ? '#0284c7' : '#475569',
                cursor: 'pointer',
                fontWeight: isCurrentPage('dining') ? 700 : 500,
                borderBottom: isCurrentPage('dining') ? '2px solid #0284c7' : 'none',
                paddingBottom: 2,
              }}
              onClick={() => navigateToPage('dining')}
            >
              {chrome.nav.dining}
            </span>
            <span
              style={{
                color: '#475569',
                cursor: 'pointer',
              }}
              onClick={() => navigateToPage('dining')}
            >
              {chrome.nav.spa}
            </span>
            <span
              style={{
                color: '#475569',
                cursor: 'pointer',
              }}
              onClick={() => navigateToPage('home')}
            >
              {chrome.nav.offers}
            </span>
          </nav>

          <div>
            <button
              type="button"
              className="btn btn-primary"
              style={{
                padding: '8px 18px',
                fontSize: '0.85rem',
                fontWeight: 600,
                borderRadius: 6,
                backgroundColor: '#0f172a',
                borderColor: '#0f172a',
              }}
              onClick={() =>
                setReservationNotice(
                  `Booking request simulated! Locale set to: ${selectedLocale} (${formatLocaleLabel(selectedLocale).name}).`
                )
              }
            >
              {chrome.nav.bookDirect}
            </button>
          </div>
        </header>

        {/* Page Content: Dynamically renders based on active CMS Content Item */}

        {/* 1. ROOMS & SUITES PAGE VIEW */}
        {isCurrentPage('rooms') && (
          <div>
            {/* Rooms Hero */}
            <section
              style={{
                position: 'relative',
                background: 'linear-gradient(135deg, #1e1b4b 0%, #312e81 50%, #1e3a8a 100%)',
                color: '#ffffff',
                padding: '60px 40px',
                textAlign: 'center',
              }}
            >
              <div style={{ maxWidth: 860, margin: '0 auto' }}>
                <div
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    backgroundColor: 'rgba(255,255,255,0.1)',
                    padding: '4px 14px',
                    borderRadius: 20,
                    fontSize: '0.8rem',
                    color: '#c7d2fe',
                    marginBottom: 16,
                  }}
                >
                  <BedDouble size={14} /> Mediterranean Haven & Panoramic Suites
                </div>

                {/* Block: rooms-hero-title */}
                {(() => {
                  const b = findBlock('rooms-hero-title');
                  return (
                    <div style={{ marginBottom: 16 }}>
                      {inspectorMode && b && (
                        <div style={{ marginBottom: 6, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                          <span className="mono" style={{ fontSize: '0.75rem', color: '#a5b4fc' }}>
                            [{b.blockKey} ➔ {b.fieldPath}]
                          </span>
                          {provenanceBadge(b.provenance)}
                        </div>
                      )}
                      <h1
                        style={{
                          fontSize: '2.6rem',
                          fontWeight: 800,
                          lineHeight: 1.2,
                          margin: 0,
                          color: '#ffffff',
                        }}
                      >
                        {b ? b.displayText : 'Luxury Suites & Oceanfront Villas'}
                      </h1>
                    </div>
                  );
                })()}

                {/* Block: rooms-hero-desc */}
                {(() => {
                  const b = findBlock('rooms-hero-desc');
                  return (
                    <div>
                      {inspectorMode && b && (
                        <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                          <span className="mono" style={{ fontSize: '0.75rem', color: '#a5b4fc' }}>
                            [{b.blockKey}]
                          </span>
                          {provenanceBadge(b.provenance)}
                        </div>
                      )}
                      <p style={{ fontSize: '1.1rem', color: '#e0e7ff', maxWidth: 680, margin: '0 auto', lineHeight: 1.6 }}>
                        {b ? b.displayText : 'Immerse yourself in panoramic Mediterranean views and private infinity plunge pools.'}
                      </p>
                    </div>
                  );
                })()}
              </div>
            </section>

            {/* Suites Showcase Cards */}
            <section style={{ padding: '50px 32px', backgroundColor: '#f8fafc' }}>
              <div style={{ maxWidth: 960, margin: '0 auto', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 24 }}>
                {/* Suite Card 1: Presidential Azure Penthouse */}
                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 14,
                    overflow: 'hidden',
                    border: '1px solid #e2e8f0',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.05)',
                    display: 'flex',
                    flexDirection: 'column',
                  }}
                >
                  <div
                    style={{
                      height: 180,
                      background: 'linear-gradient(135deg, #0369a1 0%, #0284c7 100%)',
                      position: 'relative',
                      display: 'flex',
                      alignItems: 'flex-end',
                      padding: 18,
                      color: '#ffffff',
                    }}
                  >
                    <span
                      style={{
                        backgroundColor: 'rgba(0,0,0,0.4)',
                        padding: '4px 10px',
                        borderRadius: 6,
                        fontSize: '0.75rem',
                        fontWeight: 600,
                      }}
                    >
                      Top Floor • 240 m² • 360° Riviera View
                    </span>
                  </div>

                  <div style={{ padding: 24, flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                    <div>
                      {(() => {
                        const titleBlock = findBlock('suite-azure-title');
                        return (
                          <div style={{ marginBottom: 10 }}>
                            {inspectorMode && titleBlock && (
                              <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                                  [{titleBlock.blockKey}]
                                </span>
                                {provenanceBadge(titleBlock.provenance)}
                              </div>
                            )}
                            <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#0f172a' }}>
                              {titleBlock ? titleBlock.displayText : 'The Presidential Azure Penthouse'}
                            </h3>
                          </div>
                        );
                      })()}

                      {(() => {
                        const descBlock = findBlock('suite-azure-desc');
                        return (
                          <div style={{ marginTop: 10, color: '#475569', fontSize: '0.95rem', lineHeight: 1.6 }}>
                            {inspectorMode && descBlock && (
                              <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                                  [{descBlock.blockKey}]
                                </span>
                                {provenanceBadge(descBlock.provenance)}
                              </div>
                            )}
                            {descBlock ? renderSafeContent(descBlock.displayText) : (
                              <p>Features a private rooftop terrace, infinity whirlpool, and personal sommelier service.</p>
                            )}
                          </div>
                        );
                      })()}
                    </div>

                    <div style={{ marginTop: 20, paddingTop: 16, borderTop: '1px solid #f1f5f9', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: '1.2rem', fontWeight: 800, color: '#0f172a' }}>€1,850 <span style={{ fontSize: '0.8rem', fontWeight: 400, color: '#64748b' }}>/ night</span></span>
                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        onClick={() => setReservationNotice(`Reserved Presidential Azure Penthouse in ${formatLocaleLabel(selectedLocale).name}!`)}
                      >
                        {chrome.nav.bookDirect}
                      </button>
                    </div>
                  </div>
                </div>

                {/* Suite Card 2: Riviera Royal Garden Villa */}
                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 14,
                    overflow: 'hidden',
                    border: '1px solid #e2e8f0',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.05)',
                    display: 'flex',
                    flexDirection: 'column',
                  }}
                >
                  <div
                    style={{
                      height: 180,
                      background: 'linear-gradient(135deg, #065f46 0%, #059669 100%)',
                      position: 'relative',
                      display: 'flex',
                      alignItems: 'flex-end',
                      padding: 18,
                      color: '#ffffff',
                    }}
                  >
                    <span
                      style={{
                        backgroundColor: 'rgba(0,0,0,0.4)',
                        padding: '4px 10px',
                        borderRadius: 6,
                        fontSize: '0.75rem',
                        fontWeight: 600,
                      }}
                    >
                      Private Beachfront • 310 m² • Private Garden
                    </span>
                  </div>

                  <div style={{ padding: 24, flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                    <div>
                      {(() => {
                        const titleBlock = findBlock('suite-villa-title');
                        return (
                          <div style={{ marginBottom: 10 }}>
                            {inspectorMode && titleBlock && (
                              <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                                  [{titleBlock.blockKey}]
                                </span>
                                {provenanceBadge(titleBlock.provenance)}
                              </div>
                            )}
                            <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#0f172a' }}>
                              {titleBlock ? titleBlock.displayText : 'Riviera Royal Garden Villa'}
                            </h3>
                          </div>
                        );
                      })()}

                      {(() => {
                        const descBlock = findBlock('suite-villa-desc');
                        return (
                          <div style={{ marginTop: 10, color: '#475569', fontSize: '0.95rem', lineHeight: 1.6 }}>
                            {inspectorMode && descBlock && (
                              <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                                  [{descBlock.blockKey}]
                                </span>
                                {provenanceBadge(descBlock.provenance)}
                              </div>
                            )}
                            {descBlock ? renderSafeContent(descBlock.displayText) : (
                              <p>Direct beach access with private botanical garden and dedicated 24/7 butler.</p>
                            )}
                          </div>
                        );
                      })()}
                    </div>

                    <div style={{ marginTop: 20, paddingTop: 16, borderTop: '1px solid #f1f5f9', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: '1.2rem', fontWeight: 800, color: '#0f172a' }}>€2,400 <span style={{ fontSize: '0.8rem', fontWeight: 400, color: '#64748b' }}>/ night</span></span>
                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        onClick={() => setReservationNotice(`Reserved Riviera Royal Garden Villa in ${formatLocaleLabel(selectedLocale).name}!`)}
                      >
                        {chrome.nav.bookDirect}
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              {/* Bottom CTA Banner */}
              {(() => {
                const ctaBlock = findBlock('rooms-booking-cta');
                return (
                  <div style={{ textAlign: 'center', marginTop: 36 }}>
                    {inspectorMode && ctaBlock && (
                      <div style={{ marginBottom: 6, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                        <span className="mono" style={{ fontSize: '0.75rem', color: '#64748b' }}>[{ctaBlock.blockKey}]</span>
                        {provenanceBadge(ctaBlock.provenance)}
                      </div>
                    )}
                    <button
                      type="button"
                      className="btn btn-primary"
                      style={{ padding: '12px 28px', fontSize: '1rem', fontWeight: 700 }}
                      onClick={() => setReservationNotice(`Checked Suite Availability in ${formatLocaleLabel(selectedLocale).name}!`)}
                    >
                      {ctaBlock ? ctaBlock.displayText : 'Check Suite Availability'}
                    </button>
                  </div>
                );
              })()}
            </section>
          </div>
        )}

        {/* 2. FINE DINING & SPA PAGE VIEW */}
        {isCurrentPage('dining') && (
          <div>
            {/* Dining Hero */}
            <section
              style={{
                position: 'relative',
                background: 'linear-gradient(135deg, #451a03 0%, #78350f 50%, #b45309 100%)',
                color: '#ffffff',
                padding: '60px 40px',
                textAlign: 'center',
              }}
            >
              <div style={{ maxWidth: 860, margin: '0 auto' }}>
                <div
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    backgroundColor: 'rgba(255,255,255,0.15)',
                    padding: '4px 14px',
                    borderRadius: 20,
                    fontSize: '0.8rem',
                    color: '#fed7aa',
                    marginBottom: 16,
                  }}
                >
                  <Utensils size={14} /> Michelin Star Gastronomy & Holistic Thalassotherapy
                </div>

                {/* Block: dining-hero-title */}
                {(() => {
                  const b = findBlock('dining-hero-title');
                  return (
                    <div style={{ marginBottom: 16 }}>
                      {inspectorMode && b && (
                        <div style={{ marginBottom: 6, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                          <span className="mono" style={{ fontSize: '0.75rem', color: '#fde68a' }}>
                            [{b.blockKey} ➔ {b.fieldPath}]
                          </span>
                          {provenanceBadge(b.provenance)}
                        </div>
                      )}
                      <h1
                        style={{
                          fontSize: '2.6rem',
                          fontWeight: 800,
                          lineHeight: 1.2,
                          margin: 0,
                          color: '#ffffff',
                        }}
                      >
                        {b ? b.displayText : 'Michelin Gastronomy & Azure Wellness Spa'}
                      </h1>
                    </div>
                  );
                })()}

                {/* Block: dining-hero-desc */}
                {(() => {
                  const b = findBlock('dining-hero-desc');
                  return (
                    <div>
                      {inspectorMode && b && (
                        <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                          <span className="mono" style={{ fontSize: '0.75rem', color: '#fde68a' }}>
                            [{b.blockKey}]
                          </span>
                          {provenanceBadge(b.provenance)}
                        </div>
                      )}
                      <p style={{ fontSize: '1.1rem', color: '#ffedd5', maxWidth: 680, margin: '0 auto', lineHeight: 1.6 }}>
                        {b ? b.displayText : 'An extraordinary sensory voyage crafted by world-renowned chefs and ancient holistic thalassotherapy rituals.'}
                      </p>
                    </div>
                  );
                })()}
              </div>
            </section>

            {/* Culinary & Spa Dual Showcase */}
            <section style={{ padding: '50px 32px', backgroundColor: '#f8fafc' }}>
              <div style={{ maxWidth: 960, margin: '0 auto', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 24 }}>
                {/* Dining Showcase Card */}
                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 14,
                    overflow: 'hidden',
                    border: '1px solid #e2e8f0',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.05)',
                    padding: 24,
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
                    <div style={{ width: 44, height: 44, borderRadius: 10, backgroundColor: '#fef3c7', color: '#d97706', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Utensils size={22} />
                    </div>
                    <div>
                      <span className="badge badge-amber" style={{ fontSize: '0.7rem' }}>★★★ Michelin Rated</span>
                    </div>
                  </div>

                  {(() => {
                    const titleBlock = findBlock('restaurant-title');
                    return (
                      <div style={{ marginBottom: 8 }}>
                        {inspectorMode && titleBlock && (
                          <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                              [{titleBlock.blockKey}]
                            </span>
                            {provenanceBadge(titleBlock.provenance)}
                          </div>
                        )}
                        <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#0f172a' }}>
                          {titleBlock ? titleBlock.displayText : 'Le Mirador Oceanfront Restaurant'}
                        </h3>
                      </div>
                    );
                  })()}

                  {(() => {
                    const descBlock = findBlock('restaurant-desc');
                    return (
                      <div style={{ marginTop: 10, color: '#475569', fontSize: '0.95rem', lineHeight: 1.6 }}>
                        {inspectorMode && descBlock && (
                          <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                              [{descBlock.blockKey}]
                            </span>
                            {provenanceBadge(descBlock.provenance)}
                          </div>
                        )}
                        {descBlock ? renderSafeContent(descBlock.displayText) : (
                          <p>Three-star Michelin culinary artistry highlighting wild Mediterranean seafood and organic harvest.</p>
                        )}
                      </div>
                    );
                  })()}
                </div>

                {/* Spa Showcase Card */}
                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 14,
                    overflow: 'hidden',
                    border: '1px solid #e2e8f0',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.05)',
                    padding: 24,
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
                    <div style={{ width: 44, height: 44, borderRadius: 10, backgroundColor: '#e0f2fe', color: '#0284c7', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Sparkles size={22} />
                    </div>
                    <div>
                      <span className="badge badge-blue" style={{ fontSize: '0.7rem' }}>Thermal & Thalasso</span>
                    </div>
                  </div>

                  {(() => {
                    const titleBlock = findBlock('spa-title');
                    return (
                      <div style={{ marginBottom: 8 }}>
                        {inspectorMode && titleBlock && (
                          <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                              [{titleBlock.blockKey}]
                            </span>
                            {provenanceBadge(titleBlock.provenance)}
                          </div>
                        )}
                        <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#0f172a' }}>
                          {titleBlock ? titleBlock.displayText : 'Azure Thalasso & Thermal Sanctuary'}
                        </h3>
                      </div>
                    );
                  })()}

                  {(() => {
                    const descBlock = findBlock('spa-desc');
                    return (
                      <div style={{ marginTop: 10, color: '#475569', fontSize: '0.95rem', lineHeight: 1.6 }}>
                        {inspectorMode && descBlock && (
                          <div style={{ marginBottom: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <span className="mono" style={{ fontSize: '0.72rem', color: '#64748b' }}>
                              [{descBlock.blockKey}]
                            </span>
                            {provenanceBadge(descBlock.provenance)}
                          </div>
                        )}
                        {descBlock ? renderSafeContent(descBlock.displayText) : (
                          <p>Rejuvenating seawater hydrotherapy, eucalyptus steam grottos, and custom botanical treatments.</p>
                        )}
                      </div>
                    );
                  })()}
                </div>
              </div>

              {/* Bottom CTA Banner */}
              {(() => {
                const ctaBlock = findBlock('dining-reserve-cta');
                return (
                  <div style={{ textAlign: 'center', marginTop: 36 }}>
                    {inspectorMode && ctaBlock && (
                      <div style={{ marginBottom: 6, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                        <span className="mono" style={{ fontSize: '0.75rem', color: '#64748b' }}>[{ctaBlock.blockKey}]</span>
                        {provenanceBadge(ctaBlock.provenance)}
                      </div>
                    )}
                    <button
                      type="button"
                      className="btn btn-primary"
                      style={{ padding: '12px 28px', fontSize: '1rem', fontWeight: 700, backgroundColor: '#78350f', borderColor: '#78350f' }}
                      onClick={() => setReservationNotice(`Table / Spa treatment reserved in ${formatLocaleLabel(selectedLocale).name}!`)}
                    >
                      {ctaBlock ? ctaBlock.displayText : 'Reserve a Table or Treatment'}
                    </button>
                  </div>
                );
              })()}
            </section>
          </div>
        )}

        {/* 3. HOME / DEFAULT LANDING PAGE VIEW */}
        {(!isCurrentPage('rooms') && !isCurrentPage('dining')) && (
          <div>
            {/* Hero Section */}
            <section
              style={{
                position: 'relative',
                background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 60%, #0369a1 100%)',
                color: '#ffffff',
                padding: '70px 40px',
                textAlign: 'center',
                overflow: 'hidden',
              }}
            >
              <div
                style={{
                  maxWidth: 860,
                  margin: '0 auto',
                  position: 'relative',
                  zIndex: 2,
                }}
              >
                <div
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    backgroundColor: 'rgba(255,255,255,0.1)',
                    padding: '4px 14px',
                    borderRadius: 20,
                    fontSize: '0.8rem',
                    color: '#fde68a',
                    marginBottom: 20,
                    border: '1px solid rgba(253, 230, 138, 0.25)',
                  }}
                >
                  <Sparkles size={14} /> Luxury Waterfront Escape
                </div>

                {/* Block: hero-title */}
                {(() => {
                  const heroTitle = findBlock('hero-title');
                  return (
                    <div style={{ position: 'relative', display: 'inline-block', width: '100%', marginBottom: 16 }}>
                      {inspectorMode && heroTitle && (
                        <div style={{ marginBottom: 6, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                          <span className="mono" style={{ fontSize: '0.75rem', color: '#93c5fd' }}>
                            [{heroTitle.blockKey} ➔ {heroTitle.fieldPath}]
                          </span>
                          {provenanceBadge(heroTitle.provenance)}
                        </div>
                      )}
                      <h1
                        style={{
                          fontSize: '2.8rem',
                          fontWeight: 800,
                          lineHeight: 1.2,
                          margin: 0,
                          letterSpacing: '-0.02em',
                          textShadow: '0 2px 10px rgba(0,0,0,0.3)',
                          color: '#ffffff',
                        }}
                      >
                        {heroTitle ? heroTitle.displayText : 'Welcome to our luxury hotel'}
                      </h1>
                    </div>
                  );
                })()}

                <p
                  style={{
                    fontSize: '1.15rem',
                    color: '#cbd5e1',
                    maxWidth: 640,
                    margin: '0 auto 28px auto',
                    lineHeight: 1.6,
                  }}
                >
                  Experience private oceanfront terraces, world-class gastronomy, and personalized white-glove service tailored
                  to every moment of your stay.
                </p>

                {/* Block: hero-cta */}
                {(() => {
                  const heroCta = findBlock('hero-cta');
                  return (
                    <div style={{ position: 'relative', display: 'inline-block' }}>
                      {inspectorMode && heroCta && (
                        <div style={{ marginBottom: 6, display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
                          <span className="mono" style={{ fontSize: '0.75rem', color: '#93c5fd' }}>
                            [{heroCta.blockKey}]
                          </span>
                          {provenanceBadge(heroCta.provenance)}
                        </div>
                      )}
                      <button
                        type="button"
                        onClick={() =>
                          setReservationNotice(
                            `Selected: "${heroCta?.displayText ?? 'Book your stay today'}" in ${formatLocaleLabel(selectedLocale).name}. Draft confirmed in CMS!`
                          )
                        }
                        style={{
                          backgroundColor: '#f59e0b',
                          color: '#0f172a',
                          border: 'none',
                          padding: '14px 32px',
                          fontSize: '1.05rem',
                          fontWeight: 700,
                          borderRadius: 8,
                          cursor: 'pointer',
                          boxShadow: '0 10px 25px rgba(245, 158, 11, 0.4)',
                          transition: 'all 0.2s ease',
                        }}
                      >
                        {heroCta ? heroCta.displayText : 'Book your stay today'}
                      </button>
                    </div>
                  );
                })()}
              </div>
            </section>

            {/* Featured Special Promotional Section */}
            <section style={{ padding: '40px 32px', backgroundColor: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
              <div style={{ maxWidth: 880, margin: '0 auto' }}>
                {/* Block: promo-body */}
                {(() => {
                  const promoBody = findBlock('promo-body');
                  return (
                    <div
                      style={{
                        backgroundColor: '#ffffff',
                        borderRadius: 12,
                        padding: '24px 28px',
                        border: '1px solid #e2e8f0',
                        boxShadow: '0 4px 6px -1px rgba(0,0,0,0.05)',
                        position: 'relative',
                      }}
                    >
                      {inspectorMode && promoBody && (
                        <div
                          style={{
                            marginBottom: 10,
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            borderBottom: '1px dashed #e2e8f0',
                            paddingBottom: 8,
                          }}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <span className="mono" style={{ fontSize: '0.75rem', color: '#64748b' }}>
                              Block: <code>{promoBody.blockKey}</code> ({promoBody.fieldPath})
                            </span>
                            <span className="badge badge-slate" style={{ fontSize: '0.72rem' }}>
                              {promoBody.blockType}
                            </span>
                          </div>
                          {provenanceBadge(promoBody.provenance)}
                        </div>
                      )}

                      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 16 }}>
                        <div
                          style={{
                            width: 48,
                            height: 48,
                            borderRadius: 10,
                            backgroundColor: '#e0f2fe',
                            color: '#0284c7',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            flexShrink: 0,
                          }}
                        >
                          <Utensils size={24} />
                        </div>

                        <div style={{ flex: 1 }}>
                          <div style={{ fontSize: '0.75rem', fontWeight: 700, color: '#0284c7', textTransform: 'uppercase', letterSpacing: 1 }}>
                            Exclusive Guest Privilege
                          </div>
                          <div
                            style={{
                              fontSize: '1.15rem',
                              color: '#1e293b',
                              marginTop: 4,
                              lineHeight: 1.5,
                            }}
                          >
                            {promoBody ? renderSafeContent(promoBody.displayText) : 'Enjoy complimentary breakfast during your visit.'}
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })()}

                {/* Block: amenities-banner (from v2 if present) */}
                {(() => {
                  const amenitiesBanner = findBlock('amenities-banner');
                  if (!amenitiesBanner) return null;
                  return (
                    <div
                      style={{
                        marginTop: 16,
                        backgroundColor: '#eff6ff',
                        borderRadius: 12,
                        padding: '18px 24px',
                        border: '1px solid #bfdbfe',
                        position: 'relative',
                      }}
                    >
                      {inspectorMode && (
                        <div
                          style={{
                            marginBottom: 8,
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            borderBottom: '1px dashed #bfdbfe',
                            paddingBottom: 6,
                          }}
                        >
                          <span className="mono" style={{ fontSize: '0.75rem', color: '#1e40af' }}>
                            Block: <code>{amenitiesBanner.blockKey}</code> ({amenitiesBanner.fieldPath})
                          </span>
                          {provenanceBadge(amenitiesBanner.provenance)}
                        </div>
                      )}
                      <div style={{ display: 'flex', alignItems: 'center', gap: 12, color: '#1e40af', fontWeight: 500 }}>
                        <Sparkles size={18} />
                        <span>{amenitiesBanner.displayText}</span>
                      </div>
                    </div>
                  );
                })()}
              </div>
            </section>
          </div>
        )}

        {/* Global Included World-Class Amenities (DYNAMICALLY LOCALIZED) */}
        <section style={{ padding: '40px 32px', backgroundColor: '#ffffff', borderTop: '1px solid #f1f5f9' }}>
          <div style={{ maxWidth: 880, margin: '0 auto' }}>
            <h3 style={{ textAlign: 'center', fontSize: '1.4rem', fontWeight: 700, color: '#0f172a', marginBottom: 24 }}>
              {chrome.amenitiesSectionTitle}
            </h3>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 16 }}>
              {/* WiFi */}
              <div style={{ padding: 16, borderRadius: 8, backgroundColor: '#f8fafc', border: '1px solid #f1f5f9' }}>
                <Wifi size={20} color="#0284c7" style={{ marginBottom: 8 }} />
                <div style={{ fontWeight: 600, color: '#1e293b', fontSize: '0.95rem' }}>{chrome.amenities.wifi.title}</div>
                <div style={{ color: '#64748b', fontSize: '0.8rem', marginTop: 4 }}>
                  {chrome.amenities.wifi.desc}
                </div>
              </div>

              {/* Michelin Gastronomy */}
              <div style={{ padding: 16, borderRadius: 8, backgroundColor: '#f8fafc', border: '1px solid #f1f5f9' }}>
                <Utensils size={20} color="#0284c7" style={{ marginBottom: 8 }} />
                <div style={{ fontWeight: 600, color: '#1e293b', fontSize: '0.95rem' }}>{chrome.amenities.gastronomy.title}</div>
                <div style={{ color: '#64748b', fontSize: '0.8rem', marginTop: 4 }}>
                  {chrome.amenities.gastronomy.desc}
                </div>
              </div>

              {/* Private Yacht */}
              <div style={{ padding: 16, borderRadius: 8, backgroundColor: '#f8fafc', border: '1px solid #f1f5f9' }}>
                <Compass size={20} color="#0284c7" style={{ marginBottom: 8 }} />
                <div style={{ fontWeight: 600, color: '#1e293b', fontSize: '0.95rem' }}>{chrome.amenities.yacht.title}</div>
                <div style={{ color: '#64748b', fontSize: '0.8rem', marginTop: 4 }}>
                  {chrome.amenities.yacht.desc}
                </div>
              </div>

              {/* Dedicated Butler */}
              <div style={{ padding: 16, borderRadius: 8, backgroundColor: '#f8fafc', border: '1px solid #f1f5f9' }}>
                <ShieldCheck size={20} color="#0284c7" style={{ marginBottom: 8 }} />
                <div style={{ fontWeight: 600, color: '#1e293b', fontSize: '0.95rem' }}>{chrome.amenities.butler.title}</div>
                <div style={{ color: '#64748b', fontSize: '0.8rem', marginTop: 4 }}>
                  {chrome.amenities.butler.desc}
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Global Footer (DYNAMICALLY LOCALIZED) */}
        <footer
          style={{
            padding: '24px 32px',
            backgroundColor: '#0f172a',
            color: '#94a3b8',
            fontSize: '0.8rem',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            borderTop: '1px solid #1e293b',
            flexWrap: 'wrap',
            gap: 12,
          }}
        >
          <div>
            © {new Date().getFullYear()} {chrome.footer.copyright}
          </div>
          <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
            <span>{chrome.footer.contentKeyLabel}: <code>{previewQuery.data?.externalContentKey ?? effectiveItemId}</code></span>
            <span>{chrome.footer.targetLocaleLabel}: <strong style={{ color: '#f8fafc' }}>{selectedLocale}</strong></span>
          </div>
        </footer>
      </div>
    </div>
  );
}
