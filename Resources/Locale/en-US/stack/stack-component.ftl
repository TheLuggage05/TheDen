# SPDX-FileCopyrightText: 2021 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
# SPDX-FileCopyrightText: 2021 Remie Richards <remierichards@gmail.com>
# SPDX-FileCopyrightText: 2021 ike709 <ike709@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
#
# SPDX-License-Identifier: MIT

### UI

# Shown when a stack is examined in details range
comp-stack-examine-detail-count = {$count ->
    [one] There is [color={$markupCountColor}]{$count}[/color] thing
    *[other] There are [color={$markupCountColor}]{$count}[/color] things
} in the stack.

# Stack status control
comp-stack-status = Count: [color=white]{$count}[/color]

### Interaction Messages

# Shown when attempting to add to a stack that is full
comp-stack-already-full = Stack is already full.

# Shown when a stack becomes full
comp-stack-becomes-full = Stack is now full.

# Text related to splitting a stack
comp-stack-split = You split the stack.
comp-stack-split-halve = Halve
comp-stack-split-custom = Split amount...
comp-stack-split-too-small = Stack is too small to split.

# Cherry-picked from space-station-14#32938 courtesy of Ilya246
comp-stack-split-size = Max: {$size}

ui-custom-stack-split-title = Split Amount
ui-custom-stack-split-line-edit-placeholder = Amount
ui-custom-stack-split-apply = Split
# End cherry-pick from ss14#32938