# Unity-Procedural-Map-generation
マップはprocedural generation(手続き型)で生成されます。マップをランダムに生成するために、座標グリッドを使用しました。

マップの生成は0,0,0のポイントから始まり、そこからグリッド全体にランダムに広がります。アルゴリズムは、まず0,0,0の周りのポイントをチェックし、それらのポイントが空いている場合、マップの一部を作成します。アルゴリズムが行き詰まり、最後のマップ部分の周りに利用可能なポイントがなくなると、階段を作成してマップの生成を続けます。再びアルゴリズムが行き詰まると、階段を作成して下の階に移動し、これを繰り返します。アルゴリズムは、最低限のプラットフォーム数に達するまで、マップの生成を繰り返します。
