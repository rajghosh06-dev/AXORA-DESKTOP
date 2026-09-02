import { motion } from "framer-motion";
import { TrendingUp, Award, Clock, Activity } from "lucide-react";

export function StudyAnalyticsView() {
  return (
    <div className="bg-md-surface-low border border-md-outline-variant/30 rounded-2xl p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between pb-3 border-b border-md-outline-variant/20">
        <div className="flex items-center gap-2.5">
          <Activity className="text-md-primary" size={22} />
          <h3 className="text-lg font-semibold text-md-on-surface">SM-2 Spaced Retention & Decay Curve</h3>
        </div>
        <span className="text-xs font-mono px-3 py-1 rounded-full bg-green-500/10 text-green-400 font-semibold border border-green-500/20">
          SuperMemo-2 Active
        </span>
      </div>

      {/* SVG Retention Decay Graph */}
      <div className="relative h-48 w-full bg-md-surface-container/60 rounded-xl p-4 flex flex-col justify-between overflow-hidden border border-md-outline-variant/15">
        <div className="absolute inset-0 flex items-center justify-between pointer-events-none px-6 opacity-20">
          <div className="w-full border-b border-dashed border-md-on-surface" />
        </div>

        <svg className="w-full h-full overflow-visible" viewBox="0 0 500 120">
          <defs>
            <linearGradient id="curveGradient" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#3B82F6" stopOpacity="0.4" />
              <stop offset="100%" stopColor="#3B82F6" stopOpacity="0.0" />
            </linearGradient>
          </defs>

          {/* Area fill */}
          <path
            d="M 10 20 Q 150 110 260 40 T 490 15 L 490 110 L 10 110 Z"
            fill="url(#curveGradient)"
          />

          {/* Animated Curve Line */}
          <motion.path
            d="M 10 20 Q 150 110 260 40 T 490 15"
            fill="none"
            stroke="#3B82F6"
            strokeWidth="3.5"
            strokeLinecap="round"
            initial={{ pathLength: 0 }}
            animate={{ pathLength: 1 }}
            transition={{ duration: 1.8, ease: "easeInOut" }}
          />

          {/* Key Data Points */}
          <circle cx="10" cy="20" r="5" fill="#3B82F6" />
          <circle cx="260" cy="40" r="5" fill="#10B981" />
          <circle cx="490" cy="15" r="5" fill="#8B5CF6" />
        </svg>

        <div className="flex items-center justify-between text-xs font-mono text-md-on-surface-variant z-10 pt-2">
          <span>Day 1 (Initial Review)</span>
          <span>Day 6 (SM-2 Step 2)</span>
          <span>Day 15 (Mastery Threshold)</span>
        </div>
      </div>

      {/* Metrics Row */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="p-4 rounded-xl bg-md-surface-container border border-md-outline-variant/10 flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-blue-500/10 text-blue-400">
            <TrendingUp size={20} />
          </div>
          <div>
            <p className="text-xl font-bold text-md-on-surface">92.4%</p>
            <p className="text-xs text-md-on-surface-variant">Retention Score</p>
          </div>
        </div>

        <div className="p-4 rounded-xl bg-md-surface-container border border-md-outline-variant/10 flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-emerald-500/10 text-emerald-400">
            <Award size={20} />
          </div>
          <div>
            <p className="text-xl font-bold text-md-on-surface">14 Days</p>
            <p className="text-xs text-md-on-surface-variant">Daily Review Streak</p>
          </div>
        </div>

        <div className="p-4 rounded-xl bg-md-surface-container border border-md-outline-variant/10 flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-purple-500/10 text-purple-400">
            <Clock size={20} />
          </div>
          <div>
            <p className="text-xl font-bold text-md-on-surface">2.54 EF</p>
            <p className="text-xs text-md-on-surface-variant">Avg. Easiness Factor</p>
          </div>
        </div>
      </div>
    </div>
  );
}
