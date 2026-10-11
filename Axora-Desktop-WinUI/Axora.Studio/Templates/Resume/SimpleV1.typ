// AXORA trusted resume program v1. The sole file capability is the literal below.
// Every Resume string reaches text(), never a source/path/action primitive.
#let data = json("resume.json")
#assert(data.domain == "AXORA.Resume.RenderData")
#assert(data.renderDataSchemaVersion == 1)
#assert(data.snapshotSchemaVersion == 1 and data.derivationVersion == 1)
#let d = data.document
#let f = data.formattingIntent
#assert(f.fontChoice == "explicitEmbeddedSerifV1")
#assert(f.intendedEffectiveFamily == "Libertinus Serif")
#let accent = rgb(f.accentRed, f.accentGreen, f.accentBlue)
#set page(paper: "a4", margin: f.marginInches * 1in)
#set text(font: "Libertinus Serif", fallback: false, style: "normal",
  weight: "regular", size: f.bodyPoints * 1pt, ligatures: false, hyphenate: false)
#set smartquote(enabled: false)
#set par(leading: f.leadingEm * 1em, spacing: f.itemGapEm * 1em)
#set block(spacing: f.itemGapEm * 1em)
// targetPages is a hint retained in the data. It never removes or shrinks content.
#let literal(value, heavy: false, colored: false) = {
  let lines = value.replace("\r\n", "\n").replace("\r", "\n").split("\n")
  for (index, value) in lines.enumerate() {
    if index > 0 { linebreak() }
    // Static paged-query observation of the identical argument passed to text.
    // Invisible metadata creates no label, selector, path, link or PDF action.
    metadata((domain: "AXORA.Resume.LiteralV1", value: value,
      weight: if heavy { "bold" } else { "regular" }))
    text(weight: if heavy { "bold" } else { "regular" },
      fill: if colored { accent } else { black }, value)
  }
}
#let row(parts, heavy: false, colored: false) = {
  let values = parts.filter(value => value != "")
  if values.len() > 0 {
    literal(values.join(" | "), heavy: heavy, colored: colored)
    parbreak()
  }
}
#let bullets(lines) = {
  for value in lines {
    // Empty derived lines are retained as intentional blank content.
    if value == "" { linebreak() } else { text("• "); literal(value); parbreak() }
  }
}
#let names = (summary: "Summary", education: "Education", experience: "Experience",
  skills: "Skills", projects: "Projects", certifications: "Certifications",
  achievements: "Achievements", responsibilities: "Responsibilities")
#let section-title(key) = block(above: f.itemGapEm * 1em + 0.4em, below: 0.2em, breakable: false)[
  #literal(if f.uppercaseSectionTitles { upper(names.at(key)) } else { names.at(key) },
    heavy: true, colored: true)
  #if f.showDividers { line(length: 100%, stroke: 0.5pt + accent) }
]
#block[
  #set align(if f.centerHeader { center } else { left })
  #text(size: (f.bodyPoints + 8) * 1pt, weight: "bold")[#literal(d.header.fullName, heavy: true)]
  #parbreak()
  #row((d.header.professionalTitle,))
  #row((d.header.email, d.header.phone, d.header.location))
  #row((d.header.linkedIn, d.header.linkedInUrl))
  #row((d.header.gitHub, d.header.gitHubUrl))
  #row((d.header.portfolioUrl,))
  #row((d.resumeTitle,))
]
#for key in d.visibleSectionOrder {
  assert(d.visibility.at(key))
  section-title(key)
  if key == "summary" { literal(d.summary); parbreak() }
  else if key == "education" {
    for x in d.education {
      row((x.institution,), heavy: true)
      row((x.degree, x.specialization, x.scoreOrPercentage, x.yearRange))
    }
  } else if key == "experience" {
    for x in d.experiences {
      row((x.roleTitle, x.company), heavy: true)
      row((x.location, x.startDate, x.displayEndDate))
      bullets(x.bulletLines)
      row((x.projectLink,))
    }
  } else if key == "skills" {
    for x in d.skillCategories {
      row((x.categoryName,), heavy: true)
      for value in x.entries { literal(value); parbreak() }
    }
  } else if key == "projects" {
    for x in d.projects {
      row((x.title,), heavy: true)
      row((x.techStack,))
      row((x.dateRange,))
      bullets(x.bulletLines)
      row((x.repoUrl,))
    }
  } else if key == "certifications" {
    for x in d.certifications {
      row((x.title,), heavy: true)
      row((x.issuer, x.date, x.gradeOrScore, x.credentialId))
      row((x.description,)); row((x.verificationUrl,))
    }
  } else if key == "achievements" {
    for x in d.achievements {
      row((x.title,), heavy: true)
      row((x.category, x.date)); row((x.description,)); row((x.link,))
    }
  } else if key == "responsibilities" {
    for x in d.responsibilities {
      row((x.role, x.organization), heavy: true)
      row((x.dateRange,)); bullets(x.bulletLines)
    }
  }
}

