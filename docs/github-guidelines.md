# Persistence 14 Github Structure
This document aims to outline the overall structure of the Persistence 14 github project. As the server(s) grow and we get more contributors making amazing contributions, it's become increasingly necessary to improve our organization to manage the weird challenges that are unique to running a persistent architecture. Chief among these considerations is the fact that, unlike upstream servers, we need to maintain changes which do not break the existing save. There are times, however, when such changes can improve the experience of persistence. Managing the balance between these stable and unstable changes was the core motivation behind the organizational structure described in this document.

## Overview
The github is being split into two main categories of branches:
### Server Branches
These are the branches that are being run by the servers at runtime. Automatically published to the servers before each shift, these serve as an up-to-date model of everything on a server. While server branches will be quite similar, there may be times where they diverge, such as a server reset. Currently planned are 3 server branches:
* server-us - Run by the US server
* server-eu - Run by the EU server
* server-experimental - To be run by any server running a test build. Who knows, maybe we will get a test server one day.

### Staging Branches
This is where PRs will be made. Staging branches are a place to organize and resolve conflicts ahead of time. There are three staging branches planned at this time.
* staging-stable - The default PR destination, intended for staging changes which are save-stable
* staging-unstable - A place to stage changes which are disruptive to any active save
* staging-upstream - A place to stage upstream merges

## Stable Vs. Unstable
The key feature of the new branch architecture is the separation of stable and unstable changes. As described above, the term stable/unstable refers to the *save stability* of the feature, i.e. can the feature be pushed to a server with a live save? or should it be saved for the next reset. The *vast* majority of changes made to the project are save stable, and the staging-stable branch is the new default for all PRs. Developers may redirect a change to unstable if through review or testing, the feature proves to be unstable to a save state. Below are some examples of save stable and unstable changes:

<div align="center">
<table>
  <tr>
    <th>Stable Changes</th>
    <th>Unstable Changes</th>
  </tr>
  <tr>
    <td>Changing data on a prototype</td>
    <td>Deleting or changing the ID of a prototype</td>
  </tr>
  <tr>
    <td>Adding a new system and component</td>
    <td>Deleting a system or component</td>
  </tr>
  <tr>
    <td>Adding or removing sprites</td>
    <td></td>
  </tr>
</table>
</div>

As you can probably tell, most of the unstable changes are things that *remove* content that the save is expecting. Though significantly large alterations to existing systems may result in unusual save stability and result in a feature being marked as save-unstable.

**Note**: Save stability is not a benefit or a detriment. A contribution being identified as unstable will not effect its chances of being accepted into the project. It will only determine *when* such changes may be added to the servers.

## Server Lifecycle

So many new branches, how do they all interact? Previously, all PRs were made to the same server that was run by both the US and EU servers. This is changing in a big way. 

1) PRs will be directed to either **staging-stable** or **staging-unstable**
2) All server branches will regularly merge all changes from **staging-stable**
3) **staging-unstable** will regularly merge all changes from **staging-stable**
4) When a server resets, the server branch will reset to match **staging-unstable**

Additionally, the upstream merge workflow has been updated:

1) **staging-upstream** will regularly merge all changes from **staging-unstable**
2) Upstream changes will be manually merged into **staging-upstream**
3) When all conflicts/bugs are resolved in **staging-upstream**, **staging-unstable** will merge all changes in **staging-upstream**

This will ideally be done on a more regular basis than in the past, making completion of upstream merges an easier prospect.

Server lifecycle is heavily supported with the additon of automatic executing GitHub Actions, which handle regular merging and publishing to ensure servers stay up to date with any relevant