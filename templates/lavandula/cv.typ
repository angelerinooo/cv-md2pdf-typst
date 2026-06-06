#import "./src/lavandula.typ": *

#show: lavandula-theme

#let asset-path(path) = "{{ASSETS_ROOT}}" + "/" + path

#set text(lang: "en")
#set document(
  title: "Curriculum Vitae - {{AUTHOR}}",
  author: "{{AUTHOR}}",
  date: none,
)

#cv(
  sidebar-position: "left",
  sidebar: [
    = {{AUTHOR}}
    #if "{{POSITION}}" != "" [
      ==== {{POSITION}}
    ]

    {{SIDEBAR_COMPONENTS}}
  ],
  main-content: [
    {{BODY}}
  ],
)
