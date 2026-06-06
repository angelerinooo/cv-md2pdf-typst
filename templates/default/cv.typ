#import "styles.typ": heading-size, section-size, body-size, accent, hline

#set page(margin: (x: 1.4cm, y: 1.6cm))
#set text(size: body-size)

#align(center)[
  #text(size: heading-size, weight: "bold", fill: accent)[{{AUTHOR}}]
]

#if "{{POSITION}}" != "" [
  #align(center)[
    #text(size: 11pt)[{{POSITION}}]
  ]
]

#v(0.8em)
#hline()
#v(0.8em)

{{BODY}}
