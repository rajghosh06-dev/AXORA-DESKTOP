import { useState } from "react";
import { motion } from "framer-motion";
import { invoke } from "@tauri-apps/api/core";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import {
  Brain, Sparkles, Download, ArrowRight
} from "lucide-react";
import { useToast } from "../components/ToastNotification";
import { StudyAnalyticsView } from "../components/StudyAnalyticsView";

interface Card {
  id: string;
  deck_id: String;
  question: string;
  answer: string;
  interval_days: number;
  repetition_count: number;
  easiness_factor: number;
  next_review_timestamp: number;
}

interface Deck {
  id: string;
  title: string;
  description: string;
  cards: Card[];
}

export default function FlashcardStudio() {
  const { success, error } = useToast();
  const [decks] = useState<Deck[]>([
    {
      id: "deck-1",
      title: "Computer Science & Cryptography",
      description: "AES-GCM, Argon2id, ECDH P-256 and WebSockets",
      cards: [
        {
          id: "c1",
          deck_id: "deck-1",
          question: "What does AES-GCM provide?",
          answer: "Authenticated Encryption with Associated Data (AEAD)",
          interval_days: 6,
          repetition_count: 2,
          easiness_factor: 2.5,
          next_review_timestamp: Date.now() + 86400000 * 6,
        },
        {
          id: "c2",
          deck_id: "deck-1",
          question: "What is Argon2id memory cost parameter?",
          answer: "65,536 KB (64 MB)",
          interval_days: 1,
          repetition_count: 1,
          easiness_factor: 2.5,
          next_review_timestamp: Date.now(),
        },
      ],
    },
    {
      id: "deck-2",
      title: "Android Native Development",
      description: "Jetpack Compose, Hilt, Room DB and Coroutines",
      cards: [
        {
          id: "c3",
          deck_id: "deck-2",
          question: "How do you animate graphics in Jetpack Compose?",
          answer: "graphicsLayer { rotationY = animationValue }",
          interval_days: 15,
          repetition_count: 3,
          easiness_factor: 2.6,
          next_review_timestamp: Date.now() + 86400000 * 15,
        },
      ],
    },
  ]);

  const [activeDeckId, setActiveDeckId] = useState<string>("deck-1");
  const [exporting, setExporting] = useState(false);

  const activeDeck = decks.find((d) => d.id === activeDeckId) || decks[0];

  const handleExport = async (format: "json" | "apkg") => {
    if (!activeDeck) return;
    setExporting(true);
    try {
      const selectedDir = await openDialog({
        directory: true,
        multiple: false,
        title: "Select Export Folder",
      });
      if (!selectedDir) {
        setExporting(false);
        return;
      }

      const outPath = await invoke<string>("export_flashcard_deck", {
        deck: activeDeck,
        outputDir: selectedDir as string,
        format,
      });

      success(`Exported "${activeDeck.title}" to ${format.toUpperCase()} at ${outPath}`);
    } catch (e: any) {
      error(`Export failed: ${e}`);
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="flex flex-col min-h-full space-y-6">
      {/* Header */}
      <motion.header
        initial={{ opacity: 0, y: -12 }}
        animate={{ opacity: 1, y: 0 }}
        className="flex items-center justify-between"
      >
        <div>
          <h2 className="text-3xl font-medium mb-1.5 flex items-center gap-2.5 text-md-on-surface">
            <Brain className="text-md-primary" size={28} />
            Spaced Repetition Studio
          </h2>
          <p className="text-base text-md-on-surface-variant">
            SuperMemo-2 (SM-2) Engine · AI Deck Generation · Anki Exporter
          </p>
        </div>

        <div className="flex items-center gap-3">
          <motion.button
            whileHover={{ scale: 1.03 }}
            whileTap={{ scale: 0.97 }}
            onClick={() => handleExport("json")}
            disabled={exporting}
            className="flex items-center gap-2 px-4 py-2.5 rounded-full text-sm font-medium border border-md-outline-variant/40 bg-md-surface-container hover:bg-md-surface-high text-md-on-surface transition-colors cursor-pointer"
          >
            <Download size={16} />
            Export JSON
          </motion.button>

          <motion.button
            whileHover={{ scale: 1.03 }}
            whileTap={{ scale: 0.97 }}
            onClick={() => handleExport("apkg")}
            disabled={exporting}
            className="flex items-center gap-2 px-4 py-2.5 rounded-full text-sm font-medium bg-md-primary text-md-on-primary hover:brightness-110 transition-all shadow-sm cursor-pointer"
          >
            <Sparkles size={16} />
            Export Anki (.apkg)
          </motion.button>
        </div>
      </motion.header>

      {/* Analytics & SM-2 Retention Curve View */}
      <StudyAnalyticsView />

      {/* Main Studio View */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 flex-1">
        {/* Decks List */}
        <div className="bg-md-surface-low border border-md-outline-variant/30 rounded-2xl p-5 space-y-4">
          <h3 className="text-base font-semibold text-md-on-surface flex items-center justify-between">
            <span>Decks ({decks.length})</span>
          </h3>

          <div className="space-y-2">
            {decks.map((deck) => {
              const isSelected = deck.id === activeDeckId;
              return (
                <div
                  key={deck.id}
                  onClick={() => setActiveDeckId(deck.id)}
                  className={`p-4 rounded-xl cursor-pointer border transition-all ${
                    isSelected
                      ? "bg-md-primary/10 border-md-primary text-md-on-surface"
                      : "bg-md-surface-container border-transparent hover:border-md-outline-variant/40"
                  }`}
                >
                  <p className="font-medium text-sm text-md-on-surface">{deck.title}</p>
                  <p className="text-xs text-md-on-surface-variant mt-1">{deck.description}</p>
                  <div className="mt-3 flex items-center justify-between text-xs font-mono text-md-primary">
                    <span>{deck.cards.length} cards</span>
                    <ArrowRight size={14} />
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Card Explorer */}
        <div className="lg:col-span-2 bg-md-surface-low border border-md-outline-variant/30 rounded-2xl p-5 space-y-4 flex flex-col">
          <div className="flex items-center justify-between pb-3 border-b border-md-outline-variant/20">
            <div>
              <h3 className="text-lg font-semibold text-md-on-surface">{activeDeck?.title}</h3>
              <p className="text-xs text-md-on-surface-variant">{activeDeck?.description}</p>
            </div>
          </div>

          <div className="space-y-3 flex-1 overflow-y-auto pr-1">
            {activeDeck?.cards.map((card, i) => (
              <div
                key={card.id}
                className="bg-md-surface-container border border-md-outline-variant/20 rounded-2xl p-4 space-y-2"
              >
                <div className="flex items-center justify-between text-xs text-md-primary font-semibold">
                  <span>CARD #{i + 1}</span>
                  <span>Interval: {card.interval_days}d · EF: {card.easiness_factor.toFixed(2)}</span>
                </div>
                <div className="space-y-1">
                  <p className="text-xs font-bold text-md-on-surface-variant">Q:</p>
                  <p className="text-sm font-medium text-md-on-surface">{card.question}</p>
                </div>
                <div className="space-y-1 pt-1 border-t border-md-outline-variant/10">
                  <p className="text-xs font-bold text-green-500">A:</p>
                  <p className="text-sm text-md-on-surface-variant">{card.answer}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
